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
using ArcGIS.Desktop.Framework;
using System.Windows.Input;

namespace bagis_pro
{
    public class MapTools
    {
        public static async Task<BA_ReturnCode> DisplayMaps(string strAoiPath, Layout layout, bool bInteractive)
        {
            BA_Objects.Aoi oAoi = Module1.Current.Aoi;
            if (String.IsNullOrEmpty(oAoi.Name))
            {
                if (System.IO.Directory.Exists(strAoiPath))
                {
                    // Initialize AOI object
                    GeneralTools.SetAoi(strAoiPath);
                }
                else
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("!!Please set an AOI before testing the maps", "BAGIS Pro");
                }
            }

            MapTools.DeactivateMapButtons();
            Map oMap = await MapTools.SetDefaultMapNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            if (oMap != null)
            {
                if (bInteractive == true && oMap.Layers.Count() > 0)
                {
                    string strMessage = "Adding the maps to the display will overwrite the current arrangement of data layers. " +
                           "This action cannot be undone." + System.Environment.NewLine + "Do you wish to continue ?";
                    MessageBoxResult oRes = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(strMessage, "BAGIS", MessageBoxButton.YesNo);
                    if (oRes != MessageBoxResult.Yes)
                    {
                        return BA_ReturnCode.OtherError;
                    }
                }

                if (layout == null)
                {
                    MessageBox.Show("The Basin Analysis layout could not be located. Maps will not display!", "BAGIS-PRO");
                    Module1.Current.ModuleLogManager.LogError(nameof(DisplayMaps), "The Basin Analysis layout could not be located. Maps not displayed!");
                    return BA_ReturnCode.UnknownError;
                }
                else
                {
                    BA_ReturnCode success = await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        1.0, 2.0, 7.5, 9.0);

                    //remove existing layers from map frame
                    await MapTools.RemoveLayersfromMapFrame();

                    //add aoi boundary to map
                    string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                     Constants.FILE_AOI_VECTOR;
                    Uri aoiUri = new Uri(strPath);
                    success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, Constants.MAPS_AOI_BOUNDARY);

                    //add Snotel Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SNOTEL_REPRESENTED;
                    Uri uri = new Uri(strPath);
                    CIMColor fillColor = CIMColor.CreateRGBColor(255, 0, 0, 50);    //Red with 30% transparency
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SNOTEL_REPRESENTED);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSnotel_State");

                    //add Snow Course Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SCOS_REPRESENTED;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SNOW_COURSE_REPRESENTED);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSnowCourse_State");

                    //add All Sites Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SITES_REPRESENTED;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_ALL_SITES_REPRESENTED);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSitesAll_State");

                    // add roads layer
                    Module1.Current.RoadsLayerLegend = "Within unknown distance of access road";
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ROADS_ZONE;
                    // Get buffer units out of the metadata so we can set the layer name
                    string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
                    if (!String.IsNullOrEmpty(strBagisTag))
                    {
                        string strBufferDistance = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_BUFFER_DISTANCE, ';');
                        string strBufferUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_XUNIT_VALUE, ';');
                        Module1.Current.RoadsLayerLegend = "Within " + strBufferDistance + " " + strBufferUnits + " of access road";
                    }
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Module1.Current.RoadsLayerLegend);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnRoads_State");

                    //add Public Land Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PUBLIC_LAND_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_PUBLIC_LAND);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnPublicLand_State");

                    //add Below Treeline Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_BELOW_TREELINE_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_BELOW_TREELINE);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnBelowTreeline_State");

                    //add Potential Site Locations Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SITES_LOCATION_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_SITES_LOCATION);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnSitesLocationZone_State");


                    // add aoi streams layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_STREAMS;
                    uri = new Uri(strPath);
                    await MapTools.AddLineLayerAsync(uri, Constants.MAPS_STREAMS, ColorFactory.Instance.BlueRGB);

                    // add Snotel Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOTEL;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOTEL, ColorFactory.Instance.BlueRGB,
                        SimpleMarkerStyle.X, 10);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.Aoi.HasSnotel = true;

                    // add Snow Course Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOW_COURSE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOW_COURSE, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.Star, 12);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.Aoi.HasSnowCourse = true;

                    // add hillshade layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Surfaces, true) +
                        Constants.FILE_HILLSHADE;
                    uri = new Uri(strPath);
                    await MapTools.DisplayRasterStretchSymbolAsync(uri, Constants.MAPS_HILLSHADE, "ArcGIS Colors", "Black to White", 0);

                    // add elev zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ELEV_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_ELEV_ZONE, "ArcGIS Colors",
                                "Elevation #2", "NAME", 30, true);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnElevation_State");

                    // add slope zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SLOPE_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_SLOPE_ZONE, "ArcGIS Colors",
                                "Slope", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnSlope_State");

                    // add aspect zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_ASPECT_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_ASPECT_ZONE, "ArcGIS Colors",
                                "Aspect", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnAspect_State");

                    // add SNOTEL SWE layer
                    success = await DisplaySWEMapAsync(3);

                    // add Precipitation zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_PRISM_ZONE, "ArcGIS Colors",
                               "Precipitation", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnPrism_State");

                    // create map elements
                    success = await MapTools.AddMapElements(Constants.MAPS_DEFAULT_LAYOUT_NAME);
                    success = await MapTools.DisplayNorthArrowAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);
                    success = await MapTools.DisplayScaleBarAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);

                    //zoom to aoi boundary layer
                    double bufferFactor = 1.1;
                    success = await MapTools.ZoomToExtentAsync(aoiUri, bufferFactor);
                    return success;
                }
            }
            return BA_ReturnCode.UnknownError;
        }

        public static async Task<Layout> GetDefaultLayoutAsync(string layoutName)
        {
            return await QueuedTask.Run(() =>
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

        public static async Task<BA_ReturnCode> SetDefaultMapFrameDimensionAsync(string mapFrameName, Layout oLayout, Map oMap, double xMin,
                                                                  double yMin, double xMax, double yMax)
        {
            await QueuedTask.Run(() =>
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
            return BA_ReturnCode.Success;
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
                    await FrameworkApplication.Panes.CreateMapPaneAsync(map);
                }
                return map;
            });
        }

        public static async Task<BA_ReturnCode> AddAoiBoundaryToMapAsync(Uri aoiUri, string displayName = "", double lineSymbolWidth = 1.0)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            else
            {
                return BA_ReturnCode.UnknownError;
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
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> AddPolygonLayerAsync(Uri uri, CIMColor fillColor, bool isVisible, string displayName = "")
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (uri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(uri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(uri.LocalPath);
            }

            Uri tempUri = new Uri(strFolderPath);
            bool polygonLayerExists = await GeodatabaseTools.FeatureClassExistsAsync(tempUri, strFileName);
            if (!polygonLayerExists)
            {
                return BA_ReturnCode.ReadError;
            }
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                //Define a simple renderer to symbolize the feature class.
                var simpleRender = new SimpleRendererDefinition
                {
                    SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(
                        fillColor, SimpleFillStyle.Solid,
                        SymbolFactory.Instance.ConstructStroke(ColorFactory.Instance.BlackRGB, 0))
                        .MakeSymbolReference()

                };
                //Define some of the Feature Layer's parameters
                var flyrCreatnParam = new FeatureLayerCreationParams(uri)
                {
                    Name = displayName,
                    IsVisible = isVisible,
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                // Create and apply the renderer
                CIMRenderer renderer = fLayer?.CreateRenderer(simpleRender);
                fLayer.SetRenderer(renderer);
                success = BA_ReturnCode.Success;
            });
            return success;
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
                        Module1.Current.ModuleLogManager.LogError(nameof(AddLineLayerAsync),
                            "Unable to open feature class " + strFileName);
                        Module1.Current.ModuleLogManager.LogError(nameof(AddLineLayerAsync),
                            "Exception: " + e.Message);
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
                        Module1.Current.ModuleLogManager.LogError(nameof(AddPointMarkersAsync),
                             "Unable to open feature class " + strFileName);
                        Module1.Current.ModuleLogManager.LogError(nameof(AddPointMarkersAsync),
                            "Exception: " + e.Message);
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
                        SymbolTemplate = SymbolFactory.Instance.ConstructPointSymbol(markerColor, markerSize, markerStyle)
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> ZoomToExtentAsync(Uri aoiUri, double bufferFactor = 1)
        {
            //Get the active map view.
            var mapView = MapView.Active;
            if (mapView == null)
                return BA_ReturnCode.UnknownError;
            string strFileName = null;
            string strFolderPath = null;
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }

            Envelope zoomEnv = await QueuedTask.Run<Envelope>(() =>
            {
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (
                  Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    FeatureClassDefinition fcDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(strFileName);
                    var extent = fcDefinition.GetExtent();
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "extent XMin=" + extent.XMin);
                    var expandedExtent = extent.Expand(bufferFactor, bufferFactor, true);
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "expandedExtent XMin=" + expandedExtent.XMin);
                    return expandedExtent;
                }
            });

            //Zoom the view to a given extent.
            bool bSuccess = false;
            Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "zoomEnv XMin=" + zoomEnv.XMin);
            await FrameworkApplication.Current.Dispatcher.Invoke(async () =>
            {
                // Do something on the GUI thread
                bSuccess = await MapView.Active.ZoomToAsync(zoomEnv, null);
            });


            Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "Return value from ZoomToAsync=" + bSuccess);
            if (bSuccess)
            {
                return BA_ReturnCode.Success;
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
             
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
            string[] arrLayerNames = new string[23];
            arrLayerNames[0] = Constants.MAPS_AOI_BOUNDARY;
            arrLayerNames[1] = Constants.MAPS_STREAMS;
            arrLayerNames[2] = Constants.MAPS_SNOTEL;
            arrLayerNames[3] = Constants.MAPS_SNOW_COURSE;
            arrLayerNames[4] = Constants.MAPS_HILLSHADE;
            arrLayerNames[5] = Constants.MAPS_ELEV_ZONE;
            arrLayerNames[6] = Constants.MAPS_SNOW_COURSE_REPRESENTED;
            arrLayerNames[7] = Constants.MAPS_SNOTEL_REPRESENTED;
            arrLayerNames[8] = Constants.MAPS_SLOPE_ZONE;
            arrLayerNames[9] = Constants.MAPS_ASPECT_ZONE;
            arrLayerNames[10] = Constants.MAPS_ALL_SITES_REPRESENTED;
            arrLayerNames[11] = Constants.MAPS_PRISM_ZONE;
            arrLayerNames[12] = Module1.Current.RoadsLayerLegend;
            arrLayerNames[13] = Constants.MAPS_PUBLIC_LAND;
            arrLayerNames[14] = Constants.MAPS_BELOW_TREELINE;
            arrLayerNames[15] = Constants.MAPS_SITES_LOCATION;
            int idxLayerNames = 16;
            for (int i = 0; i < Constants.LAYER_NAMES_SNODAS_SWE.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SNODAS_SWE[i];
                idxLayerNames++;
            }
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

        public static async Task DisplayRasterStretchSymbolAsync(Uri rasterUri, string displayName, string styleCategory,
            string styleName, int transparency)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            if (await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName))
            {
                await QueuedTask.Run(() =>
                {
                    // Find the color ramp
                    StyleProjectItem style =
                        Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
                    if (style == null) return;
                    var colorRampList = style.SearchColorRamps(styleName);
                    CIMColorRamp cimColorRamp = null;
                    foreach (var colorRamp in colorRampList)
                    {
                        if (colorRamp.Name.Equals(styleName))
                        {
                            cimColorRamp = colorRamp.ColorRamp;
                            break;
                        }
                    }
                    if (cimColorRamp == null)
                    {
                        return;
                    }

                    // Create a new Stretch Colorizer Definition supplying the color ramp
                    StretchColorizerDefinition stretchColorizerDef = new StretchColorizerDefinition(0, RasterStretchType.DefaultFromSource, 1.0, cimColorRamp);
                    int idxLayer = MapView.Active.Map.Layers.Count();
                    RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateRasterLayer(rasterUri, MapView.Active.Map, idxLayer,
                        displayName, stretchColorizerDef);
                    rasterLayer.SetTransparency(transparency);
                });
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterStretchSymbolAsync),
                    rasterUri.LocalPath + " could not be found. Raster not displayed!!" );
            }
        }

        public static async Task<BA_ReturnCode> DisplayRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName,
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
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithSymbolAsync),
                    "Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                    }
                    catch (Exception e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithSymbolAsync),
                            e.Message);
                    }
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToUniqueValueColorizer(displayName, styleCategory, styleName, fieldName);
                }
            });
            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> DisplayStretchRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName,
            int transparency, bool isVisible, bool useCustomMinMax, double stretchMax, double stretchMin,
            double labelMax, double labelMin)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayStretchRasterWithSymbolAsync),
                    "Unable to add locate raster!!");
                return success;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                    }
                    catch (Exception e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(DisplayStretchRasterWithSymbolAsync),
                            e.Message);
                    }
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    rasterLayer.SetTransparency(transparency);
                    rasterLayer.SetName(displayName);
                    rasterLayer.SetVisibility(isVisible);
                    // Create and deploy the unique values renderer
                    await MapTools.SetToStretchValueColorizer(displayName, styleCategory, styleName, useCustomMinMax,
                        stretchMin, stretchMax, labelMin, labelMax);
                    success = BA_ReturnCode.Success;
                }
            });
            return success;
        }

        public static async Task<BA_ReturnCode> DisplayRasterFromLayerFileAsync(Uri rasterUri, string displayName,
            string layerFilePath, int transparency, bool bIsVisible)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            if (rasterUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(rasterUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(rasterUri.LocalPath);
            }
            // Check to see if the raster exists before trying to add it
            bool bExists = await GeodatabaseTools.RasterDatasetExistsAsync(new Uri(strFolderPath), strFileName);
            if (!bExists)
            {
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterFromLayerFileAsync),
                    "Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(async () =>
            {
                RasterLayer rasterLayer = null;
                // Create the raster layer on the active map
                await QueuedTask.Run(() =>
                {
                    rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                });

                // Set raster layer transparency and name
                if (rasterLayer != null)
                {
                    //Get the Layer Document from the lyrx file
                    var lyrDocFromLyrxFile = new LayerDocument(layerFilePath);
                    var cimLyrDoc = lyrDocFromLyrxFile.GetCIMLayerDocument();

                    //Get the colorizer from the layer file
                    var layerDefs = cimLyrDoc.LayerDefinitions;
                    var colorizerFromLayerFile = ((CIMRasterLayer)cimLyrDoc.LayerDefinitions[0]).Colorizer as CIMRasterStretchColorizer;

                    //Apply the colorizer to the raster layer
                    rasterLayer?.SetColorizer(colorizerFromLayerFile);

                    //Set the name and transparency
                    rasterLayer?.SetName(displayName);
                    rasterLayer?.SetTransparency(transparency);
                    rasterLayer?.SetVisibility(bIsVisible);

                    if (rasterLayer?.GetColorizer() is CIMRasterStretchColorizer)
                    {
                        // if the stretch renderer is used get the selected band index
                        var stretchColorizer = rasterLayer?.GetColorizer() as CIMRasterStretchColorizer;
                        RasterStretchType mine = stretchColorizer.StretchType;
                    }
                }
            });
            return BA_ReturnCode.Success;
        }

        public static async Task SetToUniqueValueColorizer(string layerName, string styleCategory,
            string styleName, string fieldName)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
            if (oLayer == null)
                return;
            RasterLayer rasterLayer = (RasterLayer)oLayer;

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

        public static async Task SetToStretchValueColorizer(string layerName, string styleCategory, string styleName,
            bool useCustomMinMax, double stretchMax, double stretchMin, double labelMax, double labelMin)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
            if (oLayer == null)
                return;
            RasterLayer rasterLayer = (RasterLayer)oLayer;

            StyleProjectItem style =
                Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
            if (style == null) return;
            var colorRampList = await QueuedTask.Run(() => style.SearchColorRamps(styleName));
            if (colorRampList == null || colorRampList.Count == 0) return;
            CIMColorRamp cimColorRamp = colorRampList[0].ColorRamp;

            // Create a new Stretch Colorizer Definition supplying the color ramp
            StretchColorizerDefinition stretchColorizerDef = new StretchColorizerDefinition(0, RasterStretchType.DefaultFromSource, 1.0, cimColorRamp);
            stretchColorizerDef.StretchType = RasterStretchType.PercentMinimumMaximum;
            //Create a new Stretch colorizer using the colorizer definition created above.
            CIMRasterStretchColorizer newStretchColorizer =
              await rasterLayer.CreateColorizerAsync(stretchColorizerDef) as CIMRasterStretchColorizer;

            if (useCustomMinMax == true)
            {
                //Customize min and max
                newStretchColorizer.StretchType = RasterStretchType.MinimumMaximum;
                newStretchColorizer.StatsType = RasterStretchStatsType.GlobalStats;
                StatsHistogram histo = newStretchColorizer.StretchStats;
                histo.max = stretchMax;
                histo.min = stretchMin;
                newStretchColorizer.StretchStats = histo;

                //Update labels
                string strLabelMin = Convert.ToString(Math.Round(stretchMin, 2));
                string strLabelMax = Convert.ToString(Math.Round(stretchMax, 2));
                if (stretchMin != labelMin)
                {
                    strLabelMin = Convert.ToString(Math.Round(labelMin, 2));
                }
                if (stretchMax != labelMax)
                {
                    strLabelMax = Convert.ToString(Math.Round(labelMax, 2));
                }
                CIMRasterStretchClass[] stretchClasses = newStretchColorizer.StretchClasses;
                if (stretchClasses.Length == 3)
                {
                    stretchClasses[0].Label = strLabelMin;  // The min values are in first position
                    stretchClasses[0].Value = labelMin;
                    stretchClasses[2].Label = strLabelMax;  // The max values are in last position
                    stretchClasses[2].Value = labelMax;
                }
            }

            // Set the new colorizer on the raster layer.
            rasterLayer.SetColorizer(newStretchColorizer);
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

        public static async Task<BA_ReturnCode> AddMapElements(string layoutName)
        {
            //Finding the first project item with name matches with layoutName
            Layout layout = null;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
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
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TITLE, 4.0, 10.5, ColorFactory.Instance.BlackRGB, 20, "Times New Roman",
                    "Bold", "Title");
                // Map SubTitle
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_SUBTITLE, 4.0, 10.1, ColorFactory.Instance.BlackRGB, 20, "Times New Roman",
                    "Regular", "SubTitle");
                // (optional) textbox
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX1, 5.0, 1.0, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                    "Regular", "Text Box 1");
            }
            return success;
        }

        public static async Task<BA_ReturnCode> DisplayTextBoxAsync(Layout layout, string elementName, double xPos, double yPos,
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

            return BA_ReturnCode.Success;
        }

        public static async Task<BA_ReturnCode> DisplayLegendAsync(Layout layout, string styleCategory, string styleName)
        {
            //Construct on the worker thread
            await QueuedTask.Run(() =>
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

               // Turn off all of the layers to start
               CIMLegend cimLeg = legendElm.GetDefinition() as CIMLegend;
               foreach (CIMLegendItem legItem in cimLeg.Items)
               {
                   legItem.ShowHeading = false;
                   legItem.IsVisible = false;
               }

               // Format other elements in the legend
               cimLeg.GraphicFrame.BorderSymbol = new CIMSymbolReference
               {
                   Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 1.5, SimpleLineStyle.Solid)
               };
               cimLeg.GraphicFrame.BorderGapX = 3;
               cimLeg.GraphicFrame.BorderGapY = 3;
               // Apply the changes
               legendElm.SetDefinition(cimLeg);

           });
            return BA_ReturnCode.Success;
        }

        public async static Task<BA_ReturnCode> UpdateLegendAsync(Layout oLayout, IList<string> lstLegendLayer)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            await QueuedTask.Run(() =>
            {
                if (oLayout == null)
                {
                Project proj = Project.Current;
                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    oLayout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(UpdateLegendAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot update legend!");
                    return;
                }
              }

                //Get LayoutCIM and iterate through its elements
                var layoutDef = oLayout.GetDefinition();

                foreach (var elem in layoutDef.Elements)
                {
                    if (elem is CIMLegend)
                    {
                        var legend = elem as CIMLegend;
                        foreach (var legendItem in legend.Items)
                        {
                            if (lstLegendLayer.Contains(legendItem.Name))
                            {
                                legendItem.IsVisible = true;
                            }
                            else
                            {
                                legendItem.IsVisible = false;
                            }
                        }
                    }
                }
                //Apply the changes back to the layout
                oLayout.SetDefinition(layoutDef);
            });
            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task UpdateMapElementsAsync(string subTitleText, BA_Objects.MapDefinition mapDefinition)
        {
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                Layout layout = null;
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(UpdateMapElementsAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot update map elements!");
                    return;
                }

                if (!String.IsNullOrEmpty(mapDefinition.Title))
                {
                    if (mapDefinition.Title != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                            graphic.Text = mapDefinition.Title;
                            textBox.SetGraphic(graphic);

                        }
                    }
                    if (subTitleText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                        if (textBox != null)
                        {
                            CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                            graphic.Text = subTitleText;
                            textBox.SetGraphic(graphic);
                        }
                    }
                    if (mapDefinition.UnitsText != null)
                    {
                        GraphicElement textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                        if (textBox != null)
                        {
                            CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                            graphic.Text = mapDefinition.UnitsText;
                            textBox.SetGraphic(graphic);
                        }
                    }
                }
            });
        }

        public static async Task<BA_ReturnCode> DisplayNorthArrowAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            if (arcgis_2d != null)
            {
                await QueuedTask.Run(() =>
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
                });
                return BA_ReturnCode.Success;
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
        }

        public static async Task<BA_ReturnCode> DisplayScaleBarAsync(Layout layout, string mapFrameName)
        {
            var arcgis_2d = Project.Current.GetItems<StyleProjectItem>().First(si => si.Name == "ArcGIS 2D");

            if (arcgis_2d != null)
            {
                await QueuedTask.Run(() =>
                {
                    var scaleBars = arcgis_2d.SearchScaleBars("Alternating Scale Bar");
                if (scaleBars == null || scaleBars.Count == 0) return;
                ScaleBarStyleItem scaleBarStyleItem = scaleBars[0];

                //Reference the map frame and define the location
                MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                Coordinate2D location = new Coordinate2D(3.8, 0.3);

                //Construct the scale bar
                ScaleBar scaleBar = LayoutElementFactory.Instance.CreateScaleBar(layout, location, mapFrame, scaleBarStyleItem);
                CIMScaleBar cimScaleBar = (CIMScaleBar)scaleBar.GetDefinition();
                cimScaleBar.Divisions = 2;
                cimScaleBar.Subdivisions = 4;
                cimScaleBar.DivisionsBeforeZero = 1;
                cimScaleBar.MarkFrequency = ScaleBarFrequency.Divisions;
                cimScaleBar.MarkPosition = ScaleBarVerticalPosition.Above;
                cimScaleBar.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                scaleBar.SetDefinition(cimScaleBar);
                });
                return BA_ReturnCode.Success;
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
        }

        public static BA_Objects.MapDefinition LoadMapDefinition(BagisMapType mapType)
        {

            BA_Objects.MapDefinition mapDefinition = null;
            IList<string> lstLayers = new List<string>();
            IList<string> lstLegendLayers = new List<string>();

            switch (mapType)
            {
                case BagisMapType.ELEVATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_ELEV_ZONE };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    string strDemDisplayUnits = (string)Module1.Current.BatchToolSettings.DemDisplayUnits;
                    mapDefinition = new BA_Objects.MapDefinition("ELEVATION DISTRIBUTION",
                        "Elevation Units = " + strDemDisplayUnits, Constants.FILE_EXPORT_MAP_ELEV_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SLOPE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_SLOPE_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_SLOPE_ZONE };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SLOPE DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_SLOPE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.ASPECT:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ASPECT_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_ASPECT_ZONE };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("ASPECT DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_ASPECT_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNODAS_SWE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.LAYER_NAMES_SNODAS_SWE[3]};
                    lstLegendLayers = new List<string> { Constants.LAYER_NAMES_SNODAS_SWE[3] };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition(Constants.MAP_TITLES_SNODAS_SWE[3],
                        "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits, Constants.FILE_EXPORT_MAPS_SWE[3]);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PRISM:
                    // If we end up using this for more than PRISM, put it above the case statement so it can be shared
                    string settingsPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                        Constants.FILE_SETTINGS;
                    BA_Objects.Analysis oAnalysis = null;
                    if (System.IO.File.Exists(settingsPath))
                    {
                        using (var file = new System.IO.StreamReader(settingsPath))
                        {
                            var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                            oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                        }
                    }
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRISM_ZONE};
                    lstLegendLayers = new List<string> { Constants.MAPS_PRISM_ZONE };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    string strTitle = "PRECIPITATION DISTRIBUTION";
                    if (!String.IsNullOrEmpty(oAnalysis.PrecipZonesBegin))
                    {
                        string strPrefix = LookupTables.PrismText[oAnalysis.PrecipZonesBegin].ToUpper();
                        strTitle = strPrefix + " " + strTitle;
                    }
                    mapDefinition = new BA_Objects.MapDefinition(strTitle,
                        "Precipitation Units = Inches", Constants.FILE_EXPORT_MAP_PRECIPITATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNOTEL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOTEL_REPRESENTED};
                    lstLegendLayers = new List<string> { Constants.MAPS_SNOTEL_REPRESENTED };
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SCOS:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOW_COURSE_REPRESENTED};
                    lstLegendLayers = new List<string> { Constants.MAPS_SNOW_COURSE_REPRESENTED };
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_ALL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_ALL_SITES_REPRESENTED};
                    lstLegendLayers = new List<string> { Constants.MAPS_ALL_SITES_REPRESENTED };
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL AND SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.ROADS:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Module1.Current.RoadsLayerLegend};
                    lstLegendLayers = new List<string> { Module1.Current.RoadsLayerLegend };
                    mapDefinition = new BA_Objects.MapDefinition("PROXIMITY TO ACCESS ROAD",
                        " ", Constants.FILE_EXPORT_MAP_ROADS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PUBLIC_LAND:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_PUBLIC_LAND};
                    lstLegendLayers = new List<string> { Constants.MAPS_PUBLIC_LAND };
                    mapDefinition = new BA_Objects.MapDefinition("PUBLIC, NON-WILDERNESS LAND",
                        " ", Constants.FILE_EXPORT_MAP_PUBLIC_LAND_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.BELOW_TREELINE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_BELOW_TREELINE};
                    lstLegendLayers = new List<string> { Constants.MAPS_BELOW_TREELINE };
                    mapDefinition = new BA_Objects.MapDefinition("AREA BELOW TREELINE",
                        " ", Constants.FILE_EXPORT_MAP_BELOW_TREELINE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_LOCATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SITES_LOCATION};
                    lstLegendLayers = new List<string> { Constants.MAPS_SITES_LOCATION };
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    mapDefinition = new BA_Objects.MapDefinition("POTENTIAL SITE LOCATIONS",
                        " ", Constants.FILE_EXPORT_MAP_SITES_LOCATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;


            }
            return mapDefinition;
        }

        public static void DeactivateMapButtons()
        {
            foreach (string strButtonState in Constants.STATES_MAP_BUTTONS)
            {
                Module1.DeactivateState(strButtonState);
            }
            // if you can't use the maps, you can't export to pdf
            Module1.DeactivateState("BtnMapLoad_State");
        }

        public static async Task<BA_ReturnCode> LoadSweMapAsync(string strRaster, string strNewLayerName,
                                                                string strTitle, 
                                                                string strFileMapExport)
        {
            RasterDataset rDataset = null;
            Layer oLayer = null;
            Map map = MapView.Active.Map;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriSweGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                //Get the default map layout
                LayoutProjectItem lytItem =
                   proj.GetItems<LayoutProjectItem>()
                       .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME,
                       StringComparison.CurrentCultureIgnoreCase));
                if (lytItem != null)
                {
                    layout = lytItem.GetLayout();
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                        "Unable to find default layout!!");
                    MessageBox.Show("Unable to find default layout. Cannot display maps!");
                    return;
                }

                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(uriSweGdb)))
                {
                    // Use the geodatabase.
                    try
                    {
                        rDataset = geodatabase.OpenDataset<RasterDataset>(strRaster);
                    }
                    catch (GeodatabaseTableException e)
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                           "Unable to open raster " + strRaster);
                        Module1.Current.ModuleLogManager.LogError(nameof(LoadSweMapAsync),
                            "Exception: " + e.Message);
                        return;
                    }
                }
                oLayer = map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Module1.Current.DisplayedSweMap, StringComparison.CurrentCultureIgnoreCase));
            });

            RasterLayer rasterLayer = (RasterLayer)oLayer;
            // Create a new Stretch Colorizer Definition using the default constructor.
            StretchColorizerDefinition stretchColorizerDef_default = new StretchColorizerDefinition();
            // Create a new Stretch colorizer using the colorizer definition created above.
            CIMRasterStretchColorizer newStretchColorizer_default =
              await rasterLayer.CreateColorizerAsync(stretchColorizerDef_default) as CIMRasterStretchColorizer;

            await QueuedTask.Run(() =>
            {
                if (oLayer.CanReplaceDataSource(rDataset))
                {
                    oLayer.ReplaceDataSource(rDataset);
                    oLayer.SetName(strNewLayerName);
                }
                GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = strTitle;
                    textBox.SetGraphic(graphic);
                }
                textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = Module1.Current.Aoi.Name.ToUpper();
                    textBox.SetGraphic(graphic);
                    success = BA_ReturnCode.Success;
                }

                textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits;
                    textBox.SetGraphic(graphic);
                 }
            });

            // toggle layers according to map definition
            var allLayers = MapView.Active.Map.Layers.ToList();
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            IList<string> lstLegend = new List<string> { strNewLayerName };

            if (Module1.Current.Aoi.HasSnotel == true)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegend.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.Aoi.HasSnowCourse == true)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_SNOW_COURSE);
            }
            await QueuedTask.Run(() =>
            {
                foreach (var layer in allLayers)
                {
                    if (lstLayers.Contains(layer.Name))
                    {
                        layer.SetVisibility(true);
                    }
                    else
                    {
                        layer.SetVisibility(false);
                    }
                }
            });

            success = await MapTools.UpdateLegendAsync(layout, lstLegend);

            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                Module1.Current.DisplayedSweMap = strNewLayerName;
            }
            return success;
        }

        public static async Task<BA_ReturnCode> DisplaySWEMapAsync(int idxDefaultMonth)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
            BA_Objects.DataSource oDataSource = null;
            if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE))
            {
                oDataSource = dictLocalDataSources[Constants.DATA_TYPE_SWE];
            }
            if (oDataSource != null)
            {
                string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idxDefaultMonth];
                Uri uri = new Uri(strPath);
                double dblStretchMin = oDataSource.minValue;
                double dblStretchMax = oDataSource.maxValue;
                double dblLabelMin = dblStretchMin;
                double dblLabelMax = dblStretchMax;
                string layerUnits = "";
                string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
                if (!string.IsNullOrEmpty(strBagisTag))
                {
                    layerUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_ZUNIT_VALUE, ';');
                }
                if (string.IsNullOrEmpty(layerUnits))
                {
                    MessageBox.Show("Unable to read units from layer. Reading from local config file!!", "BAGIS-PRO");
                    layerUnits = oDataSource.units;
                }
                string strSweDisplayUnits = Module1.Current.BatchToolSettings.SweDisplayUnits;
                if (layerUnits != null && !strSweDisplayUnits.Equals(layerUnits))
                {
                    switch (strSweDisplayUnits)
                    {
                        case Constants.UNITS_INCHES:
                            dblLabelMin = LinearUnit.Millimeters.ConvertTo(dblStretchMin, LinearUnit.Inches);
                            dblLabelMax = LinearUnit.Millimeters.ConvertTo(dblStretchMax, LinearUnit.Inches);
                            break;
                        case Constants.UNITS_MILLIMETERS:
                            dblLabelMin = LinearUnit.Inches.ConvertTo(dblStretchMin, LinearUnit.Millimeters);
                            dblLabelMax = LinearUnit.Inches.ConvertTo(dblStretchMax, LinearUnit.Millimeters);
                            break;
                        default:
                            MessageBox.Show("The display units are invalid!!", "BAGIS-PRO");
                            return success;
                    }
                }

                success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.LAYER_NAMES_SNODAS_SWE[idxDefaultMonth], "ColorBrewer Schemes (RGB)",
                            "Green-Blue (Continuous)", 30, false, true, dblStretchMin, dblStretchMax, dblLabelMin,
                            dblLabelMax);
                IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri layersUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(layersUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                lstLayersFiles.Add(def.GetName());
                            }
                        }
                    });
                    int idx = 0;
                    foreach (string strSweName in Constants.FILES_SNODAS_SWE)
                    {
                        if (lstLayersFiles.Contains(strSweName))
                        {
                            switch (idx)
                            {
                                case 0:     //January
                                    Module1.ActivateState("MapButtonPalette_BtnSweJan_State");
                                    break;
                                case 1:     //February
                                    Module1.ActivateState("MapButtonPalette_BtnSweFeb_State");
                                    break;
                                case 2:     //March
                                    Module1.ActivateState("MapButtonPalette_BtnSweMar_State");
                                    break;
                                case 3:     //April
                                    Module1.ActivateState("MapButtonPalette_BtnSweApr_State");
                                    break;
                                case 4:     //May
                                    Module1.ActivateState("MapButtonPalette_BtnSweMay_State");
                                    break;
                                case 5:     //June
                                    Module1.ActivateState("MapButtonPalette_BtnSweJun_State");
                                    break;
                                case 6:     //December
                                    Module1.ActivateState("MapButtonPalette_BtnSweDec_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }

            }
            Module1.Current.DisplayedSweMap = Constants.LAYER_NAMES_SNODAS_SWE[idxDefaultMonth];
            return success;
        }

        public static async Task<BA_ReturnCode> PublishMapsAsync()
        {
            foreach (string strButtonState in Constants.STATES_MAP_BUTTONS)
            {
                if (FrameworkApplication.State.Contains(strButtonState))
                {
                    int foundS1 = strButtonState.IndexOf("_State");
                    string strMapButton = strButtonState.Remove(foundS1);
                    ICommand cmd = FrameworkApplication.GetPlugInWrapper(strMapButton) as ICommand;
                    Module1.Current.ModuleLogManager.LogDebug(nameof(PublishMapsAsync),
                        "About to toggle map button " + strMapButton);

                    if ((cmd != null))
                    {
                        do
                        {
                            await Task.Delay(TimeSpan.FromSeconds(0.4));  // build in delay until the command can execute
                        }
                        while (!cmd.CanExecute(null));
                        cmd.Execute(null);
                    }

                    do
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.4));  // build in delay so maps can load
                    }
                    while (Module1.Current.MapFinishedLoading == false);

                    BA_ReturnCode success2 = await GeneralTools.ExportMapToPdfAsync();    // export each map to pdf
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(PublishMapsAsync),
                        strButtonState + " not enabled for this AOI ");
                }
            }
            return BA_ReturnCode.Success;
        }
    }
}
