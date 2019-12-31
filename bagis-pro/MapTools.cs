﻿using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using ArcGIS.Desktop.Framework.Contracts;

namespace bagis_pro
{
    public class MapTools
    {
        public static async Task DisplayMaps(string strTempAoiPath)
        {
            BA_Objects.Aoi oAoi = Module1.Current.Aoi;
            if (String.IsNullOrEmpty(oAoi.Name))
            {
                if (System.IO.Directory.Exists(strTempAoiPath))
                {
                    // Initialize AOI object
                    oAoi = new BA_Objects.Aoi("animas_AOI_prms", strTempAoiPath);
                    // Store current AOI in Module1
                    Module1.Current.Aoi = oAoi;
                }
                else
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("!!Please set an AOI before testing the maps", "BAGIS Pro");
                }
            }

            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            if (oMap != null)
            {
                if (oMap.Layers.Count() > 0)
                {
                    string strMessage = "Adding the maps to the display will overwrite the current arrangement of data layers. " +
                           "This action cannot be undone." + System.Environment.NewLine + "Do you wish to continue ?";
                    MessageBoxResult oRes = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(strMessage, "BAGIS", MessageBoxButton.YesNo);
                    if (oRes != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                Layout layout = await MapTools.SetDefaultLayoutNameAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                if (layout != null)
                {
                    bool bFoundIt = false;
                    //A layout view may exist but it may not be active
                    //Iterate through each pane in the application and check to see if the layout is already open and if so, activate it
                    foreach (var pane in ProApp.Panes)
                    {
                        if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                            continue;
                        if (layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                        {
                            (layoutPane as Pane).Activate();
                            bFoundIt = true;
                        }
                    }
                    if (!bFoundIt)
                    {
                        ILayoutPane iNewLayoutPane = await ProApp.Panes.CreateLayoutPaneAsync(layout); //GUI thread
                    }
                    await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        1.0, 2.0, 7.5, 9.0);

                    //remove existing layers from map frame
                    await MapTools.RemoveLayersfromMapFrame();

                    //add aoi boundary to map and zoom to layer
                    string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                     Constants.FILE_AOI_VECTOR;
                    Uri aoiUri = new Uri(strPath);
                    await MapTools.AddAoiBoundaryToMapAsync(aoiUri, Constants.MAPS_AOI_BOUNDARY);

                    // add aoi streams layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_STREAMS;
                    Uri uri = new Uri(strPath);
                    await MapTools.AddLineLayerAsync(uri, Constants.MAPS_STREAMS, ColorFactory.Instance.BlueRGB);

                    // add Snotel Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOTEL;
                    uri = new Uri(strPath);
                    BA_ReturnCode success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOTEL, ColorFactory.Instance.BlueRGB,
                        SimpleMarkerStyle.X, 16);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.AoiHasSnotel = true;

                    // add Snow Course Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOW_COURSE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOW_COURSE, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.Star, 16);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.AoiHasSnowCourse = true;

                    // add hillshade layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_HILLSHADE;
                    uri = new Uri(strPath);
                    await MapTools.DisplayRasterAsync(uri, Constants.MAPS_HILLSHADE, 0);

                    // add elev zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ELEV_ZONE;
                    uri = new Uri(strPath);
                    await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_ELEV_ZONE, "ArcGIS Colors",
                                "Elevation #2", "NAME", 30, true);

                    // add slope zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SLOPE_ZONE;
                    uri = new Uri(strPath);
                    await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_SLOPE_ZONE, "ArcGIS Colors",
                                "Slope", "NAME", 30, false);


                    // create map elements
                    await MapTools.AddMapElements(Constants.MAPS_DEFAULT_LAYOUT_NAME, "ArcGIS Colors", "1.5 Point");
                    await MapTools.DisplayNorthArrowAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);
                    await MapTools.DisplayScaleBarAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);

                    // update text in map elements
                    BA_Objects.MapDefinition defaultMap = MapTools.LoadMapDefinition(BagisMapType.ELEVATION);
                    await MapTools.UpdateMapElementsAsync(layout, Module1.Current.Aoi.Name.ToUpper(), defaultMap);

                    
                    // toggle layers according to map definition
                    //var allLayers = oMap.Layers.ToList();
                    //await QueuedTask.Run(() => {
                    //    foreach (var layer in allLayers)
                    //    {

                    //        layer.SetVisibility(false);
                    //    }
                    //});

                    //zoom to aoi boundary layer
                    double bufferFactor = 1.1;
                    bool bZoomed = await MapTools.ZoomToExtentAsync(aoiUri, bufferFactor);
                }
            }
        }

        public static async Task<Layout> SetDefaultLayoutNameAsync(string layoutName)
        {
            return await QueuedTask.Run( () =>
            {
                Layout layout = null;
                Project proj = Project.Current;

                //Finding the first project item with name matches with mapName
                LayoutProjectItem lytItem =
                    proj.GetItems<LayoutProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    layout = LayoutFactory.Instance.CreateLayout(8.5, 11, LinearUnit.Inches);
                    layout.SetName(layoutName);
                }
                return layout;
            });
        }

        public static async Task SetDefaultMapFrameDimensionAsync(string mapFrameName, Layout oLayout, Map oMap, double xMin, 
                                                                  double yMin, double xMax, double yMax)
        {
            await QueuedTask.Run( () =>
            {
                //Finding the mapFrame with mapFrameName
                if (!(oLayout.FindElement(mapFrameName) is MapFrame mfElm))
                {
                    //Build 2D envelope geometry
                    Coordinate2D mf_ll = new Coordinate2D(xMin, yMin);
                    Coordinate2D mf_ur = new Coordinate2D(xMax, yMax);
                    Envelope mf_env = EnvelopeBuilder.CreateEnvelope(mf_ll, mf_ur);
                    mfElm = LayoutElementFactory.Instance.CreateMapFrame(oLayout, mf_env, oMap);
                    mfElm.SetName(mapFrameName);
                }
                // Remove border from map frame
                var mapFrameDefn = mfElm.GetDefinition() as CIMMapFrame;
                mapFrameDefn.GraphicFrame.BorderSymbol = new CIMSymbolReference
                {
                    Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 0, SimpleLineStyle.Null)
                };
                mfElm.SetDefinition(mapFrameDefn);
            });
        }

        public static async Task<Map> SetDefaultMapNameAsync(string mapName)
        {
            return await QueuedTask.Run(async () =>
            {
                Map map = null;
                Project proj = Project.Current;

                //Finding the first project item with name matches with mapName
                MapProjectItem mpi =
                    proj.GetItems<MapProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    map = mpi.GetMap();
                }
                else
                {
                    map = MapFactory.Instance.CreateMap(mapName, basemap: Basemap.None);
                    await ProApp.Panes.CreateMapPaneAsync(map);
                }
                return map;
            });
        }

        public static async Task AddAoiBoundaryToMapAsync(Uri aoiUri, string displayName = "", double lineSymbolWidth = 1.0)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            await QueuedTask.Run(() =>
            {
                FeatureClass fClass = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                }
                if (String.IsNullOrEmpty(displayName))
                {
                    displayName = fClass.GetDefinition().GetAliasName();
                }
                // Create symbology for feature layer
                var flyrCreatnParam = new FeatureLayerCreationParams(fClass)
                {
                    Name = displayName,
                    IsVisible = true,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(
                        ColorFactory.Instance.BlackRGB, SimpleFillStyle.Null,
                        SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.BlackRGB, lineSymbolWidth, SimpleLineStyle.Solid))
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
            });
        }

        public static async Task AddLineLayerAsync(Uri aoiUri, string displayName, CIMColor lineColor)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            await QueuedTask.Run(() =>
            {
                FeatureClass fClass = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Debug.WriteLine("AddLineLayerAsync: Unable to open feature class " + strFileName);
                        Debug.WriteLine("AddLineLayerAsync: " + e.Message);
                        return;
                    }
                }
                // Create symbology for feature layer
                var flyrCreatnParam = new FeatureLayerCreationParams(fClass)
                {
                    Name = displayName,
                    IsVisible = true,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructLineSymbol(lineColor)
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
            });
        }

        public static async Task<BA_ReturnCode> AddPointMarkersAsync(Uri aoiUri, string displayName, CIMColor markerColor, 
                                    SimpleMarkerStyle markerStyle, double markerSize)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                FeatureClass fClass = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Debug.WriteLine("DisplayPointMarkersAsync: Unable to open feature class " + strFileName);
                        Debug.WriteLine("DisplayPointMarkersAsync: " + e.Message);
                        success = BA_ReturnCode.ReadError;
                        return;
                    }
                }
                // Create symbology for feature layer
                var flyrCreatnParam = new FeatureLayerCreationParams(fClass)
                {
                    Name = displayName,
                    IsVisible = true,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructPointSymbol(markerColor, markerSize,  markerStyle)
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<bool> ZoomToExtentAsync(Uri aoiUri, double bufferFactor = 1)
        {            
            //Get the active map view.
            var mapView = MapView.Active;
            if (mapView == null)
                return false;
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }

            Envelope zoomEnv = null;
            await QueuedTask.Run(() => {
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (
                  Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(strFileName);
                    zoomEnv = fcDefinition.GetExtent().Expand(bufferFactor, bufferFactor, true);
                }
            });

            //Zoom the view to a given extent.
            return await mapView.ZoomToAsync(zoomEnv, null);
        }

        public static async Task RemoveLayer(Map map, string layerName)
        {
            await QueuedTask.Run(() =>
            {
                Layer oLayer =
                    map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
                if (oLayer != null)
                    map.RemoveLayer(oLayer);
            });
        }

        public static async Task RemoveLayersfromMapFrame()
        {
            string[] arrLayerNames = new string[6];
            arrLayerNames[0] = Constants.MAPS_AOI_BOUNDARY;
            arrLayerNames[1] = Constants.MAPS_STREAMS;
            arrLayerNames[2] = Constants.MAPS_SNOTEL;
            arrLayerNames[3] = Constants.MAPS_SNOW_COURSE;
            arrLayerNames[4] = Constants.MAPS_HILLSHADE;
            arrLayerNames[5] = Constants.MAPS_ELEV_ZONE;
            var map = MapView.Active.Map;
            await QueuedTask.Run(() =>
            {
                foreach (string strName in arrLayerNames)
                {
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                        map.RemoveLayer(oLayer);
                }
            });
        }

        public static async Task DisplayRasterAsync(Uri rasterUri, string displayName, int transparency)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            await QueuedTask.Run(() =>
            {
                RasterDataset rDataset = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Debug.WriteLine("DisplayRasterAsync: Unable to open raster " + strFileName);
                        Debug.WriteLine("DisplayRasterAsync: " + e.Message);
                        return;
                    }
                }
                // Create a new colorizer definition using default constructor.
                StretchColorizerDefinition stretchColorizerDef = new StretchColorizerDefinition();
                RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                }
            });
        }

        public static async Task DisplayRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName, 
            string fieldName, int transparency, bool isVisible)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterDataset rDataset = null;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strFileName);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Debug.WriteLine("DisplayRasterWithSymbolAsync: Unable to open raster " + strFileName);
                        Debug.WriteLine("DisplayRasterWithSymbolAsync: " + e.Message);
                        return;
                    }
                }
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    rasterLayer = (RasterLayer) LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToUniqueValueColorizer(displayName,styleCategory, styleName, fieldName);
                }
            });
        }

        public static async Task SetToUniqueValueColorizer(string layerName, string styleCategory, 
            string styleName, string fieldName)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
            if (oLayer == null)
                return;
            RasterLayer rasterLayer = (RasterLayer) oLayer;

            StyleProjectItem style =
                Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
            if (style == null) return;
            var colorRampList = await QueuedTask.Run(() => style.SearchColorRamps(styleName));
            if (colorRampList == null || colorRampList.Count == 0) return;
            CIMColorRamp cimColorRamp = colorRampList[0].ColorRamp;

            // Creates a new UV Colorizer Definition using the default constructor.
            UniqueValueColorizerDefinition UVColorizerDef = new UniqueValueColorizerDefinition(fieldName, cimColorRamp);

            // Creates a new UV colorizer using the colorizer definition created above.
            CIMRasterUniqueValueColorizer newColorizer = await rasterLayer.CreateColorizerAsync(UVColorizerDef) as CIMRasterUniqueValueColorizer;

            // Sets the newly created colorizer on the layer.
            await QueuedTask.Run(() =>
            {
                rasterLayer.SetColorizer(MapTools.RecalculateColorizer(newColorizer));
            });
        }

        // This method addresses the issue that the CreateColorizer does not systematically assign colors 
        // from the associated color ramp.
        // https://community.esri.com/message/867870-re-creating-unique-value-colorizer-for-raster
        public static CIMRasterColorizer RecalculateColorizer(CIMRasterColorizer colorizer)
        {
            if (colorizer is CIMRasterUniqueValueColorizer uvrColorizer)
            {
                var colorRamp = uvrColorizer.ColorRamp;
                if (colorRamp == null)
                    throw new InvalidOperationException("Colorizer must have a color ramp");

                //get the total number of colors to be assigned
                var total_colors = uvrColorizer.Groups.Select(g => g.Classes.Count()).Sum();
                var colors = ColorFactory.Instance.GenerateColorsFromColorRamp(colorRamp, total_colors);
                var c = 0;
                foreach (var uvr_group in uvrColorizer.Groups)
                {
                    foreach (var uvr_class in uvr_group.Classes)
                    {
                        //assign the generated colors to each class in turn
                        uvr_class.Color = colors[c++];
                    }
                }
            }
            else if (colorizer is CIMRasterClassifyColorizer classColorizer)
            {
                var colorRamp = classColorizer.ColorRamp;
                if (colorRamp == null)
                    throw new InvalidOperationException("Colorizer must have a color ramp");

                var total_colors = classColorizer.ClassBreaks.Count();
                var colors = ColorFactory.Instance.GenerateColorsFromColorRamp(colorRamp, total_colors);
                var c = 0;
                foreach (var cbreak in classColorizer.ClassBreaks)
                {
                    //assign the generated colors to each class break in turn
                    cbreak.Color = colors[c++];
                }
            }
            return colorizer;
        }

        public static async Task AddMapElements(string layoutName, string styleCategory, string styleName)
        {
            //Finding the first project item with name matches with layoutName
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                LayoutProjectItem lytItem =
                Project.Current.GetItems<LayoutProjectItem>()
                    .FirstOrDefault(m => m.Name.Equals(layoutName, StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
            });
            if (layout != null)
            {
                // Delete all graphic elements except map frame
                await QueuedTask.Run(() =>
                {
                    layout.DeleteElements(item => !item.Name.Contains(Constants.MAPS_DEFAULT_MAP_FRAME_NAME));
                });
                // Map Title
                await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TITLE, 4.0, 10.5, ColorFactory.Instance.BlackRGB, 24, "Times New Roman",
                    "Bold", "Title");
                // Map SubTitle
                await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_SUBTITLE, 4.0, 10.1, ColorFactory.Instance.BlackRGB, 14, "Times New Roman",
                    "Regular", "SubTitle");
                // (optional) textbox
                await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX1, 5.0, 1.0, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                    "Regular", "Text Box 1");
                // Legend
                await MapTools.DisplayLegendAsync(layout, styleCategory, styleName);
            }
        }

        public static async Task<bool> DisplayTextBoxAsync(Layout layout, string elementName, double xPos, double yPos,
                                        CIMColor fontColor, double fontSize, string fontFamily, string fontStyle, string textBoxText)
        {
            await QueuedTask.Run(() =>
            {
                //Build 2D point geometry
                Coordinate2D coord2D = new Coordinate2D(5, 5);

                //Set symbolology, create and add element to layout
                CIMTextSymbol sym = SymbolFactory.Instance.ConstructTextSymbol(fontColor, fontSize, fontFamily, fontStyle);
                sym.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Left;
                GraphicElement ptTxtElm = LayoutElementFactory.Instance.CreatePointTextGraphicElement(layout, coord2D, textBoxText, sym);
                ptTxtElm.SetName(elementName);
                ptTxtElm.SetAnchor(Anchor.CenterPoint);
                ptTxtElm.SetX(xPos);
                ptTxtElm.SetY(yPos);
            });

            return true;
        }

        public static async Task DisplayLegendAsync (Layout layout, string styleCategory, string styleName)
        {
            //Construct on the worker thread
            await QueuedTask.Run( () =>
            {
                //Build 2D envelope geometry
                Coordinate2D leg_ll = new Coordinate2D(0.5, 0.3);
                Coordinate2D leg_ur = new Coordinate2D(2.14, 2.57);
                Envelope leg_env = EnvelopeBuilder.CreateEnvelope(leg_ll, leg_ur);

                //Reference MF, create legend and add to layout
                MapFrame mapFrame = layout.FindElement(Constants.MAPS_DEFAULT_MAP_FRAME_NAME) as MapFrame;
                if (mapFrame == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Map frame not found", "WARNING");
                    return;
                }
                Legend legendElm = LayoutElementFactory.Instance.CreateLegend(layout, leg_env, mapFrame);
                legendElm.SetName(Constants.MAPS_LEGEND);
                legendElm.SetAnchor(Anchor.BottomLeftCorner);

                // Choose the items we want to show
                IList<string> lstLegend = new List<string>();
                if (Module1.Current.AoiHasSnotel == true)
                    lstLegend.Add(Constants.MAPS_SNOTEL);
                if (Module1.Current.AoiHasSnowCourse == true)
                    lstLegend.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_ELEV_ZONE);
                CIMLegend cimLeg = legendElm.GetDefinition() as CIMLegend;
                CIMLegendItem[] myLegendItems = new CIMLegendItem[lstLegend.Count];
                int i = 0;
                foreach (CIMLegendItem legItem in cimLeg.Items)
                {
                    if (lstLegend.Contains(legItem.Name))
                    {
                        legItem.ShowHeading = false;
                        myLegendItems[i] = legItem;
                        i++;
                    }
                }
                if (myLegendItems[0] != null)
                {
                    cimLeg.Items = myLegendItems;
                }

                cimLeg.GraphicFrame.BorderSymbol = new CIMSymbolReference
                {
                    Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 1.5, SimpleLineStyle.Solid)
                };
                cimLeg.GraphicFrame.BorderGapX = 3;
                cimLeg.GraphicFrame.BorderGapY = 3;
                legendElm.SetDefinition(cimLeg);

            });
        }

        public static async Task UpdateMapElementsAsync(Layout layout, string titleText, BA_Objects.MapDefinition mapDefinition)
        {
            if (layout != null)
            {
                if (!String.IsNullOrEmpty(titleText))
                {
                    if (titleText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic ) textBox.Graphic;
                                graphic.Text = titleText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                    if (mapDefinition.SubTitle != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = mapDefinition.SubTitle;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                    if (mapDefinition.UnitsText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                        if (textBox != null)
                        {
                            await QueuedTask.Run(() =>
                            {
                                CIMTextGraphic graphic = (CIMTextGraphic)textBox.Graphic;
                                graphic.Text = mapDefinition.UnitsText;
                                textBox.SetGraphic(graphic);
                            });
                        }
                    }
                }
            }
        }

        public static async Task DisplayNorthArrowAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            await QueuedTask.Run(() =>
            {
                if (arcgis_2d != null)
                {
                    var northArrowItems = arcgis_2d.SearchNorthArrows("ESRI North 1");
                    if (northArrowItems == null || northArrowItems.Count == 0) return;
                    NorthArrowStyleItem northArrowStyleItem = northArrowItems[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    Coordinate2D nArrow = new Coordinate2D(7.7906, 0.8906);

                    //Construct the north arrow
                    NorthArrow northArrow = LayoutElementFactory.Instance.CreateNorthArrow(layout, nArrow, mapFrame, northArrowStyleItem);
                    northArrow.SetHeight(0.7037);
                }
            });
        }

        public static async Task DisplayScaleBarAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            await QueuedTask.Run(() =>
            {
                if (arcgis_2d != null)
                {
                    var scaleBars = arcgis_2d.SearchScaleBars("Alternating Scale Bar");
                    if (scaleBars == null || scaleBars.Count == 0) return;
                    ScaleBarStyleItem scaleBarStyleItem = scaleBars[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    Coordinate2D location = new Coordinate2D(3.8, 0.3);

                    //Construct the scale bar
                    ScaleBar scaleBar = LayoutElementFactory.Instance.CreateScaleBar(layout, location, mapFrame, scaleBarStyleItem);
                    CIMScaleBar cimScaleBar = (CIMScaleBar) scaleBar.GetDefinition();
                    cimScaleBar.Divisions = 2;
                    cimScaleBar.Subdivisions = 4;
                    cimScaleBar.DivisionsBeforeZero = 1;
                    cimScaleBar.MarkPosition = ScaleBarVerticalPosition.Above;
                    cimScaleBar.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                    scaleBar.SetDefinition(cimScaleBar);
                    
                }
            });
        }

        public static BA_Objects.MapDefinition LoadMapDefinition(BagisMapType mapType)
        {

            BA_Objects.MapDefinition mapDefinition = null;
            IList<string> lstLayers = new List<string>();

            switch (mapType)
            {
                case BagisMapType.ELEVATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOTEL, Constants.MAPS_SNOW_COURSE};
                    // @ToDo: manage elevation units better
                    mapDefinition = new BA_Objects.MapDefinition(Constants.MAPS_ELEV_ZONE, "ELEVATION DISTRIBUTION", 
                        "Elevation Units = Feet");
                    mapDefinition.LayerList = lstLayers;
                    break; 
                case BagisMapType.SLOPE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_SLOPE_ZONE,
                                                   Constants.MAPS_SNOTEL, Constants.MAPS_SNOW_COURSE};
                    mapDefinition = new BA_Objects.MapDefinition(Constants.MAPS_SLOPE_ZONE, "SLOPE DISTRIBUTION",
                        " ");
                    mapDefinition.LayerList = lstLayers;
                    break;
            }
            return mapDefinition;
        }
    }



}
