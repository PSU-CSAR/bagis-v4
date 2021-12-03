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
        private static int IDX_STRETCH_MIN = 0;
        private static int IDX_STRETCH_MAX = 1;
        private static int IDX_LABEL_MIN = 2;
        private static int IDX_LABEL_MAX = 3;
        private static double ERROR_MIN = 9999;
        public static async Task<BA_ReturnCode> DisplayMaps(string strAoiPath, Layout layout, bool bInteractive)
        {
            BA_Objects.Aoi oAoi = Module1.Current.Aoi;
            if (String.IsNullOrEmpty(oAoi.Name))
            {
                if (System.IO.Directory.Exists(strAoiPath))
                {
                    // Initialize AOI object
                    oAoi = await GeneralTools.SetAoiAsync(strAoiPath);
                    if (oAoi != null)
                    {
                        Module1.Current.CboCurrentAoi.SetAoiName(oAoi.Name);
                        MessageBox.Show("AOI is set to " + oAoi.Name + "!", "BAGIS PRO");
                    }
                    else
                    {
                        MessageBox.Show("An error occurred while trying to set the AOI!!", "BAGIS PRO");
                    }
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

                    //retrieve Analysis object
                    BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(Module1.Current.Aoi.FilePath);

                    //add Land Ownership Layer
                    string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                        Constants.FILE_PUBLIC_LAND;
                    Uri uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerUniqueValuesAsync(uri, "ArcGIS Colors", "Basic Random",
                        new string[] { "AGBUR" }, false, false, 30.0F, Constants.MAPS_PUBLIC_LAND_OWNERSHIP);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnPublicLandOwnership_State");

                    //add aoi boundary to map
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                     Constants.FILE_AOI_VECTOR;
                    Uri aoiUri = new Uri(strPath);
                    success = await MapTools.AddAoiBoundaryToMapAsync(aoiUri, ColorFactory.Instance.BlackRGB, Constants.MAPS_DEFAULT_MAP_NAME, Constants.MAPS_AOI_BOUNDARY);

                    //add subbasin contribution layer to map                    
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                                     Constants.FILE_PRECIP_CONTRIB_VECTOR;
                    success = await MapTools.AddAoiBoundaryToMapAsync(new Uri(strPath), ColorFactory.Instance.BlackRGB, Constants.MAPS_DEFAULT_MAP_NAME, Constants.MAPS_SUBBASIN_BOUNDARY, 
                        false);

                    //add Snotel Represented Area Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_SNOTEL_REPRESENTED;
                    uri = new Uri(strPath);
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
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnPublicLandZones_State");

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
                    {
                        Module1.ActivateState("MapButtonPalette_BtnSitesLocationZone_State");
                        Module1.ActivateState("MapButtonPalette_BtnSitesLocationPrecip_State");
                        Module1.ActivateState("MapButtonPalette_BtnSitesLocationPrecipContrib_State");
                    }
                        
                    //add Critical Precipitation Zones Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_CRITICAL_PRECIP_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPolygonLayerAsync(uri, fillColor, false, Constants.MAPS_CRITICAL_PRECIPITATION_ZONES);
                    if (success.Equals(BA_ReturnCode.Success))
                        Module1.ActivateState("MapButtonPalette_BtnCriticalPrecipZone_State");

                    // add aoi streams layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_STREAMS;
                    uri = new Uri(strPath);
                    await MapTools.AddLineLayerAsync(uri, Constants.MAPS_STREAMS, ColorFactory.Instance.BlueRGB);

                    // add Snotel Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOTEL;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOTEL, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.X, 10, Constants.FIELD_SITE_ID, MaplexPointPlacementMethod.NorthEastOfPoint);
                    if (success == BA_ReturnCode.Success)
                        Module1.Current.Aoi.HasSnotel = true;

                    // add Snow Course Layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true) +
                              Constants.FILE_SNOW_COURSE;
                    uri = new Uri(strPath);
                    success = await MapTools.AddPointMarkersAsync(uri, Constants.MAPS_SNOW_COURSE, CIMColor.CreateRGBColor(0, 255, 255),
                        SimpleMarkerStyle.Star, 12, Constants.FIELD_SITE_ID, MaplexPointPlacementMethod.NorthWestOfPoint);
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

                    // add SNOTEL SWE zones layer
                    int idxDefaultMonth = 8;    // Note: This needs to be the month with the lowest SWE value for symbology; In this case July
                    success = await DisplaySWEMapAsync(idxDefaultMonth);

                    // add SWE delta layer
                    success = await DisplaySWEDeltaMapAsync(7, idxDefaultMonth);  // Note: this needs to be the month with the lowest delta value for symbology: June Delta

                    // add quarterly seasonal precipitation layer; Default is Q1
                    success = await DisplaySeasonalPrecipContribMapAsync(0);

                    // add Precipitation zones layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIP_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_PRISM_ZONE, "ArcGIS Colors",
                               "Precipitation", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnPrism_State");

                    // add winter precipitation layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_WINTER_PRECIPITATION_ZONE;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithSymbolAsync(uri, Constants.MAPS_WINTER_PRECIPITATION, "ArcGIS Colors",
                        "Precipitation", "NAME", 30, false);
                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnWinterPrecipitation_State");

                    // add Precipitation Contribution layer
                    strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true) +
                        Constants.FILE_PRECIPITATION_CONTRIBUTION;
                    uri = new Uri(strPath);
                    success = await MapTools.DisplayRasterWithClassifyAsync(uri, Constants.MAPS_PRECIPITATION_CONTRIBUTION, "ColorBrewer Schemes (RGB)",
                               "Yellow-Green-Blue (Continuous)", Constants.FIELD_VOL_ACRE_FT, 30, ClassificationMethod.EqualInterval, 10, null, null, false);

                    if (success == BA_ReturnCode.Success)
                        Module1.ActivateState("MapButtonPalette_BtnPrecipContrib_State");

                    // create map elements
                    success = await MapTools.AddMapElements(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, Constants.MAPS_DEFAULT_LAYOUT_NAME);
                    success = await MapTools.DisplayNorthArrowAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);
                    success = await MapTools.DisplayScaleBarAsync(layout, Constants.MAPS_DEFAULT_MAP_FRAME_NAME);

                    //zoom to aoi boundary layer
                    success = await MapTools.ZoomToExtentAsync(aoiUri, Constants.MAP_BUFFER_FACTOR);

                    // load AOI location map
                    success = await DisplayLocationMapAsync(oAoi);
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
            Project proj = Project.Current;
            bool bCreateMapPane = false;
            Map oMap = null;
            await QueuedTask.Run(() =>
            {
                 //Finding the first project item with name matches with mapName
                MapProjectItem mpi =
                    proj.GetItems<MapProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(mapName, StringComparison.CurrentCultureIgnoreCase));
                if (mpi != null)
                {
                    oMap = mpi.GetMap();
                }
                else
                {
                    oMap = MapFactory.Instance.CreateMap(mapName, basemap: Basemap.None);
                    bCreateMapPane = true;
                }
            });
            if (bCreateMapPane)
            {
                await ProApp.Panes.CreateMapPaneAsync(oMap);
            }
            return oMap;
        }

        public static async Task<BA_ReturnCode> AddAoiBoundaryToMapAsync(Uri aoiUri, CIMColor lineColor, string mapName = Constants.MAPS_DEFAULT_MAP_NAME,
            string displayName = "", 
            bool isVisible = true, double lineSymbolWidth = 1.0)
        {
            // parse the uri for the folder and file
            string strFileName = null;
            string strFolderPath = null;
            Map oMap = await MapTools.SetDefaultMapNameAsync(mapName);
            if (aoiUri.IsFile)
            {
                strFileName = System.IO.Path.GetFileName(aoiUri.LocalPath);
                strFolderPath = System.IO.Path.GetDirectoryName(aoiUri.LocalPath);
            }
            else
            {
                return BA_ReturnCode.UnknownError;
            }
            if (! await GeodatabaseTools.FeatureClassExistsAsync(new Uri(strFolderPath), strFileName))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(AddAoiBoundaryToMapAsync),
                    "Unable to locate feature class " + aoiUri.LocalPath + "!");
                return BA_ReturnCode.ReadError;
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
                    IsVisible = isVisible,
                    RendererDefinition = new SimpleRendererDefinition()
                    {
                        SymbolTemplate = SymbolFactory.Instance.ConstructPolygonSymbol(
                            ColorFactory.Instance.BlackRGB, SimpleFillStyle.Null,
                        SymbolFactory.Instance.ConstructStroke(lineColor, lineSymbolWidth, SimpleLineStyle.Solid))
                        .MakeSymbolReference()
                    }
                };

                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, oMap);
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

        public static async Task<BA_ReturnCode> AddPolygonLayerUniqueValuesAsync(Uri uri, string styleCategory, string styleName,
                                                                                 string[] arrDisplayFields, bool isVisible, 
                                                                                 bool bAllOtherValues, double dblTransparency, string displayName = "")
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
                // Find the color ramp
                StyleProjectItem style =
                    Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
                if (style == null) return;
                var colorRampList = style.SearchColorRamps(styleName);
                CIMColorRamp cimColorRamp = null;
                if (colorRampList.Count > 0)
                {
                    cimColorRamp = colorRampList[0].ColorRamp;
                }
                else
                {
                    return;
                }

                //construct unique value renderer definition 
                var oSymbolReference = SymbolFactory.Instance.ConstructPolygonSymbol(ColorFactory.Instance.RedRGB, SimpleFillStyle.Solid, null).MakeSymbolReference();
                UniqueValueRendererDefinition uvr = new
                   UniqueValueRendererDefinition()
                {
                    ValueFields = arrDisplayFields, //multiple fields in the array if needed.
                    ColorRamp = cimColorRamp, //Specify color ramp
                    SymbolTemplate = oSymbolReference,
                    UseDefaultSymbol = bAllOtherValues
                };

                //Define some of the Feature Layer's parameters
                var flyrCreatnParam = new FeatureLayerCreationParams(uri)
                {
                    Name = displayName,
                    IsVisible = isVisible,
                };
                FeatureLayer fLayer = LayerFactory.Instance.CreateLayer<FeatureLayer>(flyrCreatnParam, MapView.Active.Map);
                if (dblTransparency > 0)
                {
                    fLayer.SetTransparency(dblTransparency);
                }

                //Creates a "Renderer"
                var cimRenderer = fLayer.CreateRenderer(uvr);
                
                //Sets the renderer to the feature layer
                fLayer.SetRenderer(cimRenderer);
                
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
                                    SimpleMarkerStyle markerStyle, double markerSize, string labelField, 
                                    MaplexPointPlacementMethod mapPlacementMethod)
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
                int idxLabelField = -1;
                // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                using (Geodatabase geodatabase =
                    new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(strFolderPath))))
                {
                    // Use the geodatabase.
                    try
                    {
                        fClass = geodatabase.OpenDataset<FeatureClass>(strFileName);
                        FeatureClassDefinition featureClassDefinition = fClass.GetDefinition();
                        idxLabelField = featureClassDefinition.FindField(labelField);
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

                if (fLayer != null && idxLabelField > -1)
                {
                    fLayer.SetLabelVisibility(true);   //set the label's visiblity
                    //Get the layer's definition
                    var lyrDefn = fLayer.GetDefinition() as CIMFeatureLayer;
                    //Get the label classes - we need the first one
                    var listLabelClasses = lyrDefn.LabelClasses.ToList();
                    var theLabelClass = listLabelClasses.FirstOrDefault();
                    //Select label field
                    theLabelClass.Expression = "return $feature." + labelField + ";";
                    //Modify label Placement 
                    //Check if the label engine is Maplex or standard.
                    CIMGeneralPlacementProperties labelEngine = MapView.Active.Map.GetDefinition().GeneralPlacementProperties;
                    if (labelEngine is CIMStandardGeneralPlacementProperties) //Current labeling engine is Standard labeling engine               
                        theLabelClass.StandardLabelPlacementProperties.PointPlacementMethod = StandardPointPlacementMethod.OnTopPoint;
                    else
                    {
                        //Current labeling engine is Maplex labeling engine            
                        theLabelClass.MaplexLabelPlacementProperties.PointPlacementMethod = mapPlacementMethod;
                        theLabelClass.MaplexLabelPlacementProperties.NeverRemoveLabel = true;
                    }
                    //Gets the text symbol of the label class            
                    var textSymbol = listLabelClasses.FirstOrDefault().TextSymbol.Symbol as CIMTextSymbol; 
                    textSymbol.FontStyleName = "Bold"; //set font as bold
                    textSymbol.SetSize(14); //set font size 
                    lyrDefn.LabelClasses = listLabelClasses.ToArray(); //Set the labelClasses back
                    fLayer.SetDefinition(lyrDefn); //set the layer's definition
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(AddPointMarkersAsync),
                        "Field " + labelField + " is missing from the feature class and cannot be used as a label!");
                }
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

            bool bSuccess = false;
            await QueuedTask.Run(() =>
            {
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
                    Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "zoomEnv XMin=" + zoomEnv.XMin);
                    bSuccess = MapView.Active.ZoomTo(expandedExtent, null);
                }
            });


            //Zoom the view to a given extent.
            //Module1.Current.ModuleLogManager.LogDebug(nameof(ZoomToExtentAsync), "zoomEnv XMin=" + zoomEnv.XMin);
            //await FrameworkApplication.Current.Dispatcher.Invoke(async () =>
            //{
            //    // Do something on the GUI thread
            //    bSuccess = await MapView.Active.ZoomToAsync(zoomEnv, null);
            //});


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

        public static async Task RemoveLayersfromMapFrame()
        {
            string[] arrLayerNames = new string[41];
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
            arrLayerNames[12] = Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES;
            arrLayerNames[13] = Constants.MAPS_BELOW_TREELINE;
            arrLayerNames[14] = Constants.MAPS_SITES_LOCATION;
            arrLayerNames[15] = Constants.MAPS_CRITICAL_PRECIPITATION_ZONES;
            arrLayerNames[16] = Constants.MAPS_PUBLIC_LAND_OWNERSHIP;
            arrLayerNames[17] = Constants.MAPS_PRECIPITATION_CONTRIBUTION;
            arrLayerNames[18] = Constants.MAPS_WINTER_PRECIPITATION;
            arrLayerNames[19] = Constants.MAPS_SUBBASIN_BOUNDARY;
            int idxLayerNames = 20;
            for (int i = 0; i < Constants.LAYER_NAMES_SNODAS_SWE.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SNODAS_SWE[i];
                idxLayerNames++;
            }
            for (int i = 0; i < Constants.LAYER_NAMES_SWE_DELTA.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SWE_DELTA[i];
                idxLayerNames++;
            }
            for (int i = 0; i < Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB.Length; i++)
            {
                arrLayerNames[idxLayerNames] = Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[i];
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
                    {

                        map.RemoveLayer(oLayer);
                    }
                }


                //special handling for the roads zones layer because the name may be different between AOI's; It's based on the buffer distance
                var returnLayers =
                     map.Layers.Where(m => m.Name.Contains("Within"));
                IList<string> lstLayerNames = new List<string>();
                foreach (var item in returnLayers)
                {
                    lstLayerNames.Add(item.Name);
                }
                foreach (var strName in lstLayerNames)
                {
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {

                        map.RemoveLayer(oLayer);
                    }
                }
           });
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

        public static async Task<BA_ReturnCode> DisplayRasterWithClassifyAsync(Uri rasterUri, string displayName, string styleCategory, 
            string styleName, string fieldName, int transparency, ClassificationMethod classificationMethod, int numClasses, IList<BA_Objects.Interval> lstInterval,
            int[,] arrColors, bool isVisible)
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
                Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithClassifyAsync),
                    "Unable to add locate raster!!");
                return BA_ReturnCode.ReadError;
            }
            // Create the raster layer on the active map
            await QueuedTask.Run(() =>
            {
                try
                {
                    RasterLayer rasterLayer = (RasterLayer)LayerFactory.Instance.CreateLayer(rasterUri, MapView.Active.Map);
                    // Set raster layer transparency and name
                    if (rasterLayer != null)
                    {
                        rasterLayer.SetName(displayName);
                        rasterLayer.SetTransparency(transparency);
                        rasterLayer.SetVisibility(isVisible);
                    }
                }
                catch (Exception e)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(DisplayRasterWithClassifyAsync),
                        e.Message);
                }
            });

            // Create and deploy the classify renderer
            if (lstInterval == null || lstInterval.Count == 0)
            {
                await MapTools.SetToClassifyRenderer(displayName, styleCategory, styleName, fieldName,
                    classificationMethod, numClasses);
            }
            else
            {
                await MapTools.SetToClassifyRenderer(displayName, fieldName, lstInterval, arrColors);
            }

            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> DisplayStretchRasterWithSymbolAsync(Uri rasterUri, string displayName, string styleCategory, string styleName,
            int transparency, bool isVisible, bool useCustomMinMax, double stretchMax, double stretchMin,
            double labelMax, double labelMin, bool bDisplayBackgroundValue)
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
                        stretchMin, stretchMax, labelMin, labelMax, bDisplayBackgroundValue);
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

        public static async Task SetToClassifyRenderer(string layerName, string styleCategory,
            string styleName, string fieldName, ClassificationMethod classificationMethod, int numberofClasses)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));
 
            BasicRasterLayer basicRasterLayer = null;
            if (oLayer != null && oLayer is BasicRasterLayer)
            {
                basicRasterLayer = (BasicRasterLayer)oLayer;
            }
            else
            {
                return;
            }

            StyleProjectItem style =
                Project.Current.GetItems<StyleProjectItem>().FirstOrDefault(s => s.Name == styleCategory);
            if (style == null) return;
            var colorRampList = await QueuedTask.Run(() => style.SearchColorRamps(styleName));
            if (colorRampList == null || colorRampList.Count == 0) return;
            CIMColorRamp cimColorRamp = colorRampList[0].ColorRamp;

            // Creates a new Classify Colorizer Definition using defined parameters.
            ClassifyColorizerDefinition classifyColorizerDef = 
                new ClassifyColorizerDefinition(fieldName, numberofClasses, classificationMethod, cimColorRamp);

            // Creates a new Classify colorizer using the colorizer definition created above.
            CIMRasterClassifyColorizer newColorizer = 
                await basicRasterLayer.CreateColorizerAsync(classifyColorizerDef) as CIMRasterClassifyColorizer;

            // Sets the newly created colorizer on the layer.
            await QueuedTask.Run(() =>
            {
                basicRasterLayer.SetColorizer(newColorizer);
            });
        }

        public static async Task SetToClassifyRenderer(string layerName, string fieldName, IList<BA_Objects.Interval> lstInterval,
            int[,] arrColors)
        {
            // Get the layer we want to symbolize from the map
            Layer oLayer =
                MapView.Active.Map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(layerName, StringComparison.CurrentCultureIgnoreCase));

            BasicRasterLayer basicRasterLayer = null;
            if (oLayer != null && oLayer is BasicRasterLayer)
            {
                basicRasterLayer = (BasicRasterLayer)oLayer;
            }
            else
            {
                return;
            }

            CIMColorRamp cimColorRamp = await CreateCustomColorRampAsync(arrColors);

            // Creates a new Classify Colorizer Definition using defined parameters.
            ClassifyColorizerDefinition classifyColorizerDef =
                new ClassifyColorizerDefinition(fieldName, lstInterval.Count, ClassificationMethod.Manual, cimColorRamp);

            // Creates a new Classify colorizer using the colorizer definition created above.
            CIMRasterClassifyColorizer newColorizer =
                await basicRasterLayer.CreateColorizerAsync(classifyColorizerDef) as CIMRasterClassifyColorizer;

            // Sets the newly created colorizer on the layer.
            await QueuedTask.Run(() =>
            {
                basicRasterLayer.SetColorizer(newColorizer);
                var json = newColorizer.ToJson();
                dynamic jsonColorizer = Newtonsoft.Json.Linq.JObject.Parse(json);
                Newtonsoft.Json.Linq.JArray arrClassBreaks = (Newtonsoft.Json.Linq.JArray)jsonColorizer["classBreaks"];
                int i = 0;
                // Customize the upper bound and label for the class breaks
                foreach (dynamic classBreak in arrClassBreaks)
                {
                    var nextInterval = lstInterval[i];
                    classBreak.upperBound = nextInterval.UpperBound;
                    classBreak.label = nextInterval.Name;
                    i++;
                }
                string stringjson = Newtonsoft.Json.JsonConvert.SerializeObject(jsonColorizer);
                newColorizer = CIMRasterClassifyColorizer.FromJson(stringjson);
                basicRasterLayer.SetColorizer(newColorizer);
            });
        }

        public static async Task SetToStretchValueColorizer(string layerName, string styleCategory, string styleName,
            bool useCustomMinMax, double stretchMax, double stretchMin, double labelMax, double labelMin, bool bDisplayBackgroundValue)
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
            stretchColorizerDef.DisplayBackgroundValue = bDisplayBackgroundValue;
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

        public static async Task<BA_ReturnCode> AddMapElements(string mapFrameName, string layoutName, bool bSitesTableDescr = true)
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
                    layout.DeleteElements(item => !item.Name.Contains(mapFrameName));
                });
                // Map Title
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TITLE, 4.0, 10.5, ColorFactory.Instance.BlackRGB, 20, "Times New Roman",
                    "Bold", "Title");
                // Map SubTitle
                success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_SUBTITLE, 4.0, 10.1, ColorFactory.Instance.BlackRGB, 20, "Times New Roman",
                    "Regular", "SubTitle");
                if (bSitesTableDescr)
                {
                    // (optional) textbox
                    success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX1, 5.0, 1.6, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                        "Regular", "Text Box 1");
                    // sites textbox
                    success = await MapTools.DisplayTextBoxAsync(layout, Constants.MAPS_TEXTBOX2, 5.2, 0.35, ColorFactory.Instance.BlackRGB, 12, "Times New Roman",
                        "Regular", Constants.TEXT_SITES_TABLE_DESCR);
                }
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

        public static async Task<BA_ReturnCode> DisplayLegendAsync(string mapFrameName, Layout layout, string styleCategory, string styleName,
            bool bHideAllLayers)
        {
            //Construct on the worker thread
            await QueuedTask.Run(() =>
            {
               //Build 2D envelope geometry
               Coordinate2D leg_ll = new Coordinate2D(0.5, 0.3);
               Coordinate2D leg_ur = new Coordinate2D(2.14, 2.57);
               Envelope leg_env = EnvelopeBuilder.CreateEnvelope(leg_ll, leg_ur);

               //Reference MF, create legend and add to layout
               MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
               var layoutDef = layout.GetDefinition();
               var legend = layoutDef.Elements.OfType<Legend>().FirstOrDefault();
               if (legend == null)
               {
                   legend = LayoutElementFactory.Instance.CreateLegend(layout, leg_env, mapFrame);
               }
               legend.SetName(Constants.MAPS_LEGEND);
               legend.SetAnchor(Anchor.BottomLeftCorner);

               
               CIMLegend cimLeg = legend.GetDefinition() as CIMLegend;
                // Turn off all of the layers to start
                if (bHideAllLayers)
                {
                    foreach (CIMLegendItem legItem in cimLeg.Items)
                    {
                        legItem.ShowHeading = false;
                        legItem.IsVisible = false;
                    }
                }

               // Format other elements in the legend
               cimLeg.GraphicFrame.BorderSymbol = new CIMSymbolReference
               {
                   Symbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.BlackRGB, 1.5, SimpleLineStyle.Solid)
               };
               cimLeg.GraphicFrame.BorderGapX = 3;
               cimLeg.GraphicFrame.BorderGapY = 3;
                //cimLeg.FittingStrategy = LegendFittingStrategy.AdjustFrame;

                // Apply the changes
                legend.SetDefinition(cimLeg);

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
                var legend = layoutDef.Elements.OfType<CIMLegend>().FirstOrDefault();
                if (legend != null)
                {
                    if (lstLegendLayer.Count > 0)
                    {
                        CIMLegendItem[] arrTempItems = new CIMLegendItem[legend.Items.Length];
                        int idx = 0;
                        //  Add items that we want to display to temporary array
                        foreach (var strName in lstLegendLayer)
                        {
                            foreach (var legendItem in legend.Items)
                            {
                                // Find the item in the existing array
                                if (legendItem.Name == strName)
                                {
                                    // Set the visibility
                                    legendItem.IsVisible = true;
                                    // Add the item to the temporary array
                                    arrTempItems[idx] = legendItem;
                                    // Increment the index
                                    idx++;
                                }
                            }
                        }
                        // Add the remaining items with visibility set to false in their original order
                        foreach (var legendItem in legend.Items)
                        {
                            if (!lstLegendLayer.Contains(legendItem.Name))
                            {
                                legendItem.IsVisible = false;
                                arrTempItems[idx] = legendItem;
                                // Increment the index
                                idx++;
                            }
                        }
                        // Set the legend items
                        legend.Items = arrTempItems;

                        // Set legend fitting strategy to accommodate longer classification list if elevation
                        // Otherwise adjust columns and size to make it fit
                        if (lstLegendLayer.Contains(Constants.MAPS_ELEV_ZONE))
                        {
                            legend.FittingStrategy = LegendFittingStrategy.AdjustFrame;
                        }
                        else
                        {
                            legend.FittingStrategy = LegendFittingStrategy.AdjustColumnsAndSize;
                        }
                        legend.Visible = true;
                    }
                    else
                    {
                        legend.Visible = false;
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
                    Coordinate2D nArrow = new Coordinate2D(7.7906, 0.9906);

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
                    var scaleBars = arcgis_2d.SearchScaleBars("Scale Line ");
                    if (scaleBars == null || scaleBars.Count == 0) return;
                        ScaleBarStyleItem scaleBarStyleItem = scaleBars[0];

                    //Reference the map frame and define the location
                    MapFrame mapFrame = layout.FindElement(mapFrameName) as MapFrame;
                    double coordX = 3.7732;
                    Coordinate2D location = new Coordinate2D(coordX, 1.0975);

                    //Construct the scale bar
                    ScaleBar scaleBar = LayoutElementFactory.Instance.CreateScaleBar(layout, location, mapFrame, scaleBarStyleItem);
                    CIMScaleBar cimScaleBar = (CIMScaleBar)scaleBar.GetDefinition();
                    cimScaleBar.MarkPosition = ScaleBarVerticalPosition.Above;
                    cimScaleBar.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                    //cimScaleBar.AlignToZeroPoint = true;
                    scaleBar.SetDefinition(cimScaleBar);

                    // Second scale bar for kilometers
                    // https://support.esri.com/en/technical-article/000011784
                    // This article recommended setting AlignToZeroPoint to true but when I set this, the scale bars aren't added to
                    // the map. Bug? Current version: 2.63
                    var scaleBars2 = arcgis_2d.SearchScaleBars("Scale Line 3");
                    if (scaleBars2 == null || scaleBars2.Count == 0) return;
                    ScaleBarStyleItem scaleBarStyleItem2 = scaleBars2[0];

                    //Define the location
                    Coordinate2D location2 = new Coordinate2D(coordX, 0.8035);

                    //Construct the scale bar
                    ScaleBar scaleBar2 = LayoutElementFactory.Instance.CreateScaleBar(layout, location2, mapFrame, scaleBarStyleItem2);
                    CIMScaleBar cimScaleBar2 = (CIMScaleBar)scaleBar2.GetDefinition();
                    cimScaleBar2.Units = LinearUnit.Kilometers;
                    cimScaleBar2.UnitLabel = cimScaleBar2.Units.Name + "s";
                    cimScaleBar2.UnitLabelPosition = ScaleBarLabelPosition.AfterLabels;
                    //cimScaleBar2.AlignToZeroPoint = true;
                    scaleBar2.SetDefinition(cimScaleBar2);

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

            // Get Analysis object for maps that need it
            BA_Objects.Analysis oAnalysis = new BA_Objects.Analysis();
            if (mapType == BagisMapType.PRISM || mapType == BagisMapType.WINTER_PRECIPITATION)
            {
                string settingsPath = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAPS + "\\" +
                    Constants.FILE_SETTINGS;
                if (System.IO.File.Exists(settingsPath))
                {
                    using (var file = new System.IO.StreamReader(settingsPath))
                    {
                        var reader = new System.Xml.Serialization.XmlSerializer(typeof(BA_Objects.Analysis));
                        oAnalysis = (BA_Objects.Analysis)reader.Deserialize(file);
                    }
                }
            }

            switch (mapType)
            {
                case BagisMapType.ELEVATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_ELEV_ZONE);

                    string strDemDisplayUnits = (string)Module1.Current.BatchToolSettings.DemDisplayUnits;
                    mapDefinition = new BA_Objects.MapDefinition("ELEVATION DISTRIBUTION",
                        "Elevation Units = " + strDemDisplayUnits, Constants.FILE_EXPORT_MAP_ELEV_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SLOPE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_SLOPE_ZONE};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_SLOPE_ZONE);
                    mapDefinition = new BA_Objects.MapDefinition("SLOPE DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_SLOPE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.ASPECT:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ASPECT_ZONE};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_ASPECT_ZONE);
                    mapDefinition = new BA_Objects.MapDefinition("ASPECT DISTRIBUTION",
                        " ", Constants.FILE_EXPORT_MAP_ASPECT_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SNODAS_SWE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.LAYER_NAMES_SNODAS_SWE[5]};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.LAYER_NAMES_SNODAS_SWE[5]);
                    mapDefinition = new BA_Objects.MapDefinition(Constants.MAP_TITLES_SNODAS_SWE[5],
                        "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits, Constants.FILE_EXPORT_MAPS_SWE[5]);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PRISM:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRISM_ZONE};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_PRISM_ZONE);
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
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnotel == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOTEL);
                        lstLegendLayers.Add(Constants.MAPS_SNOTEL);
                    }
                    lstLegendLayers.Add(Constants.MAPS_SNOTEL_REPRESENTED);
                    mapDefinition = new BA_Objects.MapDefinition("SNOTEL SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SNOTEL_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SCOS:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SNOW_COURSE_REPRESENTED};
                    lstLegendLayers = new List<string>();
                    if (Module1.Current.Aoi.HasSnowCourse == true)
                    {
                        lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                        lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE);
                    }
                    lstLegendLayers.Add(Constants.MAPS_SNOW_COURSE_REPRESENTED);
                    mapDefinition = new BA_Objects.MapDefinition("SNOW COURSE SITES REPRESENTATION",
                        " ", Constants.FILE_EXPORT_MAP_SCOS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_ALL:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_ALL_SITES_REPRESENTED};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_ALL_SITES_REPRESENTED);
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
                    mapDefinition = new BA_Objects.MapDefinition("PROXIMITY TO ACCESS ROAD",
                        " ", Constants.FILE_EXPORT_MAP_ROADS_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PUBLIC_LAND_ZONES:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES};
                    lstLegendLayers = new List<string> { Constants.MAPS_FEDERAL_PUBLIC_LAND_ZONES };
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
                    mapDefinition = new BA_Objects.MapDefinition("FEDERAL NON-WILDERNESS LAND",
                        " ", Constants.FILE_EXPORT_MAP_PUBLIC_LAND_ZONES_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.BELOW_TREELINE:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_BELOW_TREELINE};
                    lstLegendLayers = new List<string> { Constants.MAPS_BELOW_TREELINE };
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
                    mapDefinition = new BA_Objects.MapDefinition("AREA BELOW TREELINE",
                        " ", Constants.FILE_EXPORT_MAP_BELOW_TREELINE_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_LOCATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_SITES_LOCATION};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_SITES_LOCATION);
                    lstLegendLayers.Add(Constants.MAPS_ELEV_ZONE);
                    mapDefinition = new BA_Objects.MapDefinition("POTENTIAL SITE LOCATIONS",
                        " ", Constants.FILE_EXPORT_MAP_SITES_LOCATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_LOCATION_PRECIP:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRISM_ZONE,
                                                   Constants.MAPS_SITES_LOCATION};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_SITES_LOCATION);
                    lstLegendLayers.Add(Constants.MAPS_PRISM_ZONE);
                    mapDefinition = new BA_Objects.MapDefinition("POTENTIAL SITE LOCATIONS",
                        " ", Constants.FILE_EXPORT_MAP_SITES_LOCATION_PRECIP_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.SITES_LOCATION_PRECIP_CONTRIB:
                    lstLayers = new List<string> { Constants.MAPS_SUBBASIN_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRECIPITATION_CONTRIBUTION,
                                                   Constants.MAPS_SITES_LOCATION, Constants.MAPS_AOI_BOUNDARY};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_SITES_LOCATION);
                    lstLegendLayers.Add(Constants.MAPS_PRECIPITATION_CONTRIBUTION);
                    mapDefinition = new BA_Objects.MapDefinition("POTENTIAL SITE LOCATIONS",
                        " ", Constants.FILE_EXPORT_MAP_SITES_LOCATION__PRECIP_CONTRIB_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.PRECIPITATION_CONTRIBUTION:
                    lstLayers = new List<string> { Constants.MAPS_SUBBASIN_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PRECIPITATION_CONTRIBUTION,
                                                    Constants.MAPS_AOI_BOUNDARY};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_PRECIPITATION_CONTRIBUTION);
                    mapDefinition = new BA_Objects.MapDefinition("SUBBASIN ANNUAL PRECIPITATION CONTRIBUTION",
                        "Units = Acre Feet", Constants.FILE_EXPORT_MAP_PRECIPITATION_CONTRIBUTION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.CRITICAL_PRECIP:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_ELEV_ZONE,
                                                   Constants.MAPS_CRITICAL_PRECIPITATION_ZONES};
                    lstLegendLayers = new List<string> { Constants.MAPS_CRITICAL_PRECIPITATION_ZONES, Constants.MAPS_ELEV_ZONE };
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
                    mapDefinition = new BA_Objects.MapDefinition("CRITICAL PRECIPITATION ZONES",
                        " ", Constants.FILE_EXPORT_MAP_CRITICAL_PRECIPITATION_ZONES_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;

                case BagisMapType.PUBLIC_LAND_OWNERSHIP:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_PUBLIC_LAND_OWNERSHIP };
                    lstLegendLayers = new List<string> { Constants.MAPS_PUBLIC_LAND_OWNERSHIP };
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
                    mapDefinition = new BA_Objects.MapDefinition("PUBLIC LAND OWNERSHIP",
                        " ", Constants.FILE_EXPORT_MAP_PUBLIC_LAND_OWNERSHIP_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
                case BagisMapType.WINTER_PRECIPITATION:
                    lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                   Constants.MAPS_HILLSHADE, Constants.MAPS_WINTER_PRECIPITATION};
                    lstLegendLayers = new List<string>();
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
                    lstLegendLayers.Add(Constants.MAPS_WINTER_PRECIPITATION);
                    strTitle = "WINTER PRECIPITATION";
                    if (!String.IsNullOrEmpty(oAnalysis.WinterStartMonth))
                    {
                        string strSuffix = " (" + oAnalysis.WinterStartMonth.ToUpper() + " - " + 
                            oAnalysis.WinterEndMonth.ToUpper() + ")";
                        strTitle = strTitle + strSuffix;
                    }
                    mapDefinition = new BA_Objects.MapDefinition(strTitle,
                        "Precipitation Units = Inches", Constants.FILE_EXPORT_MAP_WINTER_PRECIPITATION_PDF);
                    mapDefinition.LayerList = lstLayers;
                    mapDefinition.LegendLayerList = lstLegendLayers;
                    break;
            }
            return mapDefinition;
        }

        public static void DeactivateMapButtons()
        {
            foreach (string strButtonState in Constants.STATES_WATERSHED_MAP_BUTTONS)
            {
                Module1.DeactivateState(strButtonState);
            }
            //foreach (string strButtonState in Constants.STATES_SITE_ANALYSIS_MAP_BUTTONS)
            //{
            //    Module1.DeactivateState(strButtonState);
            //}
            // if you can't use the maps, you can't export to pdf
            Module1.DeactivateState("BtnMapLoad_State");
        }

        public static async Task<BA_ReturnCode> LoadSweMapAsync(string strRaster, string strNewLayerName,
                                                                string strTitle, string strFileMapExport)
        {
            RasterDataset rDataset = null;
            Layer oLayer = null;
            Map map = null;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriSweGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                // Set map to Basin Analysis map
                map = Project.Current.GetItems<MapProjectItem>().FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME)).GetMap();

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
                    graphic.Text = Module1.Current.Aoi.NwccName.ToUpper();
                    textBox.SetGraphic(graphic);
                    success = BA_ReturnCode.Success;
                }

                textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    if (graphic != null)
                    {        
                        graphic.Text = "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits;
                    }
                    textBox.SetGraphic(graphic);
                 }
            });

            // toggle layers according to map definition
            var allLayers = map.Layers.ToList();
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            IList<string> lstLegend = new List<string>();

            if (Module1.Current.Aoi.HasSnotel)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegend.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.Aoi.HasSnowCourse)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_SNOW_COURSE);
            }
            lstLegend.Add(strNewLayerName);
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

            if (layout != null)
            {
                foreach (var pane in FrameworkApplication.Panes)
                {
                    if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                        continue;
                    if (layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                    {
                        (layoutPane as Pane).Activate();
                    }
                }
            }

            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                Module1.Current.DisplayedSweMap = strNewLayerName;                
            }
            return success;
        }

        public static async Task<BA_ReturnCode> LoadSweDeltaMapAsync(string strRaster, string strNewLayerName,
                                                                     string strTitle, string strFileMapExport)
        {
            RasterDataset rDataset = null;
            Layer oLayer = null;
            Map map = null;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriSweGdb = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;

                // Set map to Basin Analysis map
                map = Project.Current.GetItems<MapProjectItem>().FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME)).GetMap();

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

                oLayer = map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(Module1.Current.DisplayedSweDeltaMap, StringComparison.CurrentCultureIgnoreCase));
            });

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
                    graphic.Text = Module1.Current.Aoi.NwccName.ToUpper();
                    textBox.SetGraphic(graphic);
                    success = BA_ReturnCode.Success;
                }

                textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    if (graphic != null)
                    {
                        graphic.Text = "Depth Units = " + Module1.Current.BatchToolSettings.SweDisplayUnits +
                                       "\r\nSource = SNODAS";
                    }
                    textBox.SetGraphic(graphic);
                }
            });

            // toggle layers according to map definition
            var allLayers = map.Layers.ToList();
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            IList<string> lstLegend = new List<string>();

            if (Module1.Current.Aoi.HasSnotel)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegend.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.Aoi.HasSnowCourse)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_SNOW_COURSE);
            }
            lstLegend.Add(strNewLayerName);
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

            if (layout != null)
            {
                foreach (var pane in FrameworkApplication.Panes)
                {
                    if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                        continue;
                    if (layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                    {
                        (layoutPane as Pane).Activate();
                    }
                }
            }

            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                Module1.Current.DisplayedSweDeltaMap = strNewLayerName;
            }
            return success;
        }

        public static async Task<BA_ReturnCode> UpdateMapAsync(string strGeodatabasePath, string strRaster, string strOldLayerName,
                                                               string strNewLayerName, string strTitle, string strUnitsText,
                                                               bool bDisplaySites, string strFileMapExport)
        {
            RasterDataset rDataset = null;
            Layer oLayer = null;
            Map map = null;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriGdb = new Uri(strGeodatabasePath);
            Layout layout = null;
            await QueuedTask.Run(() =>
            {
                Project proj = Project.Current;
                // Get the Basin Analysis map and set it active
                map = Project.Current.GetItems<MapProjectItem>().FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_MAP_NAME)).GetMap();

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
                    new Geodatabase(new FileGeodatabaseConnectionPath(uriGdb)))
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
                oLayer = map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strOldLayerName, StringComparison.CurrentCultureIgnoreCase));
            });

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
                    graphic.Text = Module1.Current.Aoi.NwccName.ToUpper();
                    textBox.SetGraphic(graphic);
                    success = BA_ReturnCode.Success;
                }

                textBox = layout.FindElement(Constants.MAPS_TEXTBOX1) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    if (graphic != null)
                    {
                        graphic.Text = strUnitsText;
                    }
                    textBox.SetGraphic(graphic);
                }
            });

            // toggle layers according to map definition
            var allLayers = map.Layers.ToList();
            IList<string> lstLayers = new List<string> { Constants.MAPS_AOI_BOUNDARY, Constants.MAPS_STREAMS,
                                                         Constants.MAPS_HILLSHADE, strNewLayerName};
            IList<string> lstLegend = new List<string>();

            if (Module1.Current.Aoi.HasSnotel == true && bDisplaySites)
            {
                lstLayers.Add(Constants.MAPS_SNOTEL);
                lstLegend.Add(Constants.MAPS_SNOTEL);
            }
            if (Module1.Current.Aoi.HasSnowCourse == true && bDisplaySites)
            {
                lstLayers.Add(Constants.MAPS_SNOW_COURSE);
                lstLegend.Add(Constants.MAPS_SNOW_COURSE);
            }
            lstLegend.Add(strNewLayerName);
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
            if (layout != null)
            {
                foreach (var pane in FrameworkApplication.Panes)
                {
                    if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                        continue;
                    if (layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                    {
                        (layoutPane as Pane).Activate();
                    }
                }
            }

            if (success == BA_ReturnCode.Success)
            {
                Module1.Current.DisplayedMap = strFileMapExport;
                Module1.Current.DisplayedSeasonalPrecipContribMap = strNewLayerName;
            }
            return success;
        }


        public static async Task<BA_ReturnCode> DisplaySWEMapAsync(int idxDefaultMonth)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                Constants.FILES_SNODAS_SWE[idxDefaultMonth];
            Uri uri = new Uri(strPath);

            IList<BA_Objects.Interval> lstInterval = await CalculateSweZonesAsync(idxDefaultMonth);
            success = await MapTools.DisplayRasterWithClassifyAsync(uri, Constants.LAYER_NAMES_SNODAS_SWE[idxDefaultMonth], "",
                "", "NAME", 30, ClassificationMethod.Manual, lstInterval.Count, lstInterval, Constants.ARR_SWE_COLORS, false);
            IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
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
                                case 0:     //November
                                    Module1.ActivateState("MapButtonPalette_BtnSweNov_State");
                                    break;
                                case 1:     //December
                                    Module1.ActivateState("MapButtonPalette_BtnSweDec_State");
                                    break;
                                case 2:     //January
                                    Module1.ActivateState("MapButtonPalette_BtnSweJan_State");
                                    break;
                                case 3:     //February
                                    Module1.ActivateState("MapButtonPalette_BtnSweFeb_State");
                                    break;
                                case 4:     //March
                                    Module1.ActivateState("MapButtonPalette_BtnSweMar_State");
                                    break;
                                case 5:     //April
                                    Module1.ActivateState("MapButtonPalette_BtnSweApr_State");
                                    break;
                                case 6:     //May
                                    Module1.ActivateState("MapButtonPalette_BtnSweMay_State");
                                    break;
                                case 7:     //June
                                    Module1.ActivateState("MapButtonPalette_BtnSweJun_State");
                                    break;
                                case 8:     //July
                                    Module1.ActivateState("MapButtonPalette_BtnSweJul_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }
                Module1.Current.DisplayedSweMap = Constants.LAYER_NAMES_SNODAS_SWE[idxDefaultMonth];
            return success;
        }

        public static async Task<double[]> SWEUnitsConversionAsync(string strDataType, int idxDefaultMonth)
        {
            double[] arrReturnValues = new double[4];
            IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
            BA_Objects.DataSource oValuesDataSource = null;
            if (dictLocalDataSources.ContainsKey(strDataType))
            {
               oValuesDataSource = dictLocalDataSources[strDataType];
            }
            if (oValuesDataSource != null)
            {
                double dblStretchMin = oValuesDataSource.minValue;
                double dblStretchMax = oValuesDataSource.maxValue;
                string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                    Constants.FILES_SNODAS_SWE[idxDefaultMonth];
                string layerUnits = await QuerySweLayerUnitsAsync(strPath);
                string strSweDisplayUnits = Module1.Current.BatchToolSettings.SweDisplayUnits;
                if (layerUnits != null && !strSweDisplayUnits.Equals(layerUnits))
                {
                    double dblLabelMin = dblStretchMin;
                    double dblLabelMax = dblStretchMax;
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

                            Module1.Current.ModuleLogManager.LogError(nameof(SWEUnitsConversionAsync),
                                "The display units are invalid!!");
                            break;
                    }
                    arrReturnValues[IDX_STRETCH_MIN] = dblStretchMin;
                    arrReturnValues[IDX_STRETCH_MAX] = dblStretchMax;
                    arrReturnValues[IDX_LABEL_MIN] = dblLabelMin;
                    arrReturnValues[IDX_LABEL_MAX] = dblLabelMax;
                }
            }
            else
            {
                arrReturnValues[IDX_STRETCH_MIN] = ERROR_MIN;
            }
            return arrReturnValues;
        }

        public static async Task<string> QuerySweLayerUnitsAsync(string strPath)
        {
            string strBagisTag = await GeneralTools.GetBagisTagAsync(strPath, Constants.META_TAG_XPATH);
            string layerUnits = Constants.UNITS_MILLIMETERS;
                if (!string.IsNullOrEmpty(strBagisTag))
            {
                layerUnits = GeneralTools.GetValueForKey(strBagisTag, Constants.META_TAG_ZUNIT_VALUE, ';');
            }
            if (string.IsNullOrEmpty(layerUnits))
            {
                Module1.Current.ModuleLogManager.LogError(nameof(QuerySweLayerUnitsAsync),
                    "Unable to read units from layer. Reading from local config file!!");
                IDictionary<string, BA_Objects.DataSource> dictLocalDataSources = GeneralTools.QueryLocalDataSources();
                BA_Objects.DataSource oUnitsDataSource = null;
                if (dictLocalDataSources.ContainsKey(Constants.DATA_TYPE_SWE))
                {
                    oUnitsDataSource = dictLocalDataSources[Constants.DATA_TYPE_SWE];
                }
                if (oUnitsDataSource != null)
                {
                    layerUnits = oUnitsDataSource.units;
                }                
            }
            return layerUnits;
        }

        public static async Task<BA_ReturnCode> DisplaySWEDeltaMapAsync(int idxDefaultDeltaMonth, int idxDefaultUnitsMonth)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILES_SWE_DELTA[idxDefaultDeltaMonth];
            Uri uri = new Uri(strPath);

            IList<BA_Objects.Interval> lstInterval = await CalculateSweDeltaZonesAsync(idxDefaultUnitsMonth);
            success = await MapTools.DisplayRasterWithClassifyAsync(uri, Constants.LAYER_NAMES_SWE_DELTA[idxDefaultDeltaMonth], "",
                "", "NAME", 30, ClassificationMethod.Manual, lstInterval.Count, lstInterval, Constants.ARR_SWE_DELTA_COLORS, false);

            //success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.LAYER_NAMES_SWE_DELTA[idxDefaultDeltaMonth], "ColorBrewer Schemes (RGB)",
            //    "Red-Blue (Continuous)", 30, false, true, dblStretchMin, dblStretchMax, dblLabelMin,
            //    dblLabelMax, true);
            IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                lstLayersFiles.Add(def.GetName());
                            }
                        }
                    });
                    int idx = 0;
                    foreach (string strSweName in Constants.FILES_SWE_DELTA)
                    {
                        if (lstLayersFiles.Contains(strSweName))
                        {
                            switch (idx)
                            {
                                case 0:     //Dec - Nov SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweNovDelta_State");
                                    break;
                                case 1:     //Jan - Dec SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweDecDelta_State");
                                    break;
                                case 2:     //Feb - Jan SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweJanDelta_State");
                                    break;
                                case 3:     //Mar - Feb SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweFebDelta_State");
                                    break;
                                case 4:     //Apr - Mar SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweMarDelta_State");
                                    break;
                                case 5:     //May - Apr SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweAprDelta_State");
                                    break;
                                case 6:     //Jun - May SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweMayDelta_State");
                                    break;
                                case 7:     //Jul - Jun SWE
                                    Module1.ActivateState("MapButtonPalette_BtnSweJunDelta_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }
                Module1.Current.DisplayedSweDeltaMap = Constants.LAYER_NAMES_SWE_DELTA[idxDefaultDeltaMonth];
            return success;
        }

        public static async Task<BA_ReturnCode> DisplaySeasonalPrecipContribMapAsync(int idxDefaultMonth)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) +
                Constants.FILES_SEASON_PRECIP_CONTRIB[idxDefaultMonth];
            Uri uri = new Uri(strPath);

            IList<BA_Objects.Interval> lstInterval = CalculateSeasonalPrecipZones();
            //int[,] arrColors = null;
            //if (lstInterval.Count < Constants.ARR_SWE_DELTA_COLORS.GetLength(0))
            //{
            //    arrColors = new int[lstInterval.Count, 4];
            //    int idxSource = 3;
            //    for (int i = 0; i < arrColors.GetLength(0); i++)
            //    {
            //        for (int j = 0; j < 4; j++)
            //        {
            //            arrColors[i, j] = Constants.ARR_SWE_DELTA_COLORS[idxSource, j];
            //        }
            //        idxSource++;
            //    }
            //}
            //else
            //{
            //    arrColors = Constants.ARR_SWE_DELTA_COLORS;
            //}
            success = await MapTools.DisplayRasterWithClassifyAsync(uri, Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxDefaultMonth], "",
                "", "NAME", 30, ClassificationMethod.Manual, lstInterval.Count, lstInterval, Constants.ARR_SWE_COLORS, false);


            //success = await MapTools.DisplayStretchRasterWithSymbolAsync(uri, Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxDefaultMonth], "ColorBrewer Schemes (RGB)",
            //        "Yellow-Orange-Red (continuous)", 30, false, true, 0, dblMaxPercent, 0, Math.Ceiling(dblMaxPercent), false);

            IList<string> lstLayersFiles = new List<string>();
                if (success == BA_ReturnCode.Success)
                {
                    await QueuedTask.Run(() =>
                    {
                        // Opens a file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                        Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis));
                        using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                        {
                            IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                            foreach (RasterDatasetDefinition def in definitions)
                            {
                                lstLayersFiles.Add(def.GetName());
                            }
                        }
                    });
                    int idx = 0;
                    foreach (string strSqName in Constants.FILES_SEASON_PRECIP_CONTRIB)
                    {
                        if (lstLayersFiles.Contains(strSqName))
                        {
                            switch (idx)
                            {
                                case 0:     //SQ1
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ1_State");
                                    break;
                                case 1:     //SQ2
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ2_State");
                                    break;
                                case 2:     //SQ3
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ3_State");
                                    break;
                                case 3:     //SQ4
                                    Module1.ActivateState("MapButtonPalette_BtnSeasonalPrecipContribSQ4_State");
                                    break;
                            }
                        }
                        idx++;
                    }
                }
                Module1.Current.DisplayedSeasonalPrecipContribMap = Constants.LAYER_NAMES_SEASON_PRECIP_CONTRIB[idxDefaultMonth];
            return success;
        }

        public static async Task<BA_ReturnCode> PublishMapsAsync(ReportType rType)
        {
            string[] arrStates = Constants.STATES_WATERSHED_MAP_BUTTONS;
            //if (rType.Equals(ReportType.SiteAnalysis))
            //{
            //    arrStates = Constants.STATES_SITE_ANALYSIS_MAP_BUTTONS;
            //}
            foreach (string strButtonState in arrStates)
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

                    BA_ReturnCode success2 = await GeneralTools.ExportMapToPdfAsync(150);    // export each map to pdf
                }
                else
                {
                    Module1.Current.ModuleLogManager.LogDebug(nameof(PublishMapsAsync),
                        strButtonState + " not enabled for this AOI ");
                }
            }
            return BA_ReturnCode.Success;
        }

    public static async Task<BA_ReturnCode> DisplayMapServiceLayerAsync(string strMapName, Uri mapUri, string displayName,
        bool bIsVisible)
        {
            Layer layer = null;
            Map oMap = await MapTools.SetDefaultMapNameAsync(strMapName);

            // Open the requested raster so we know it exists; return if it doesn't
            await QueuedTask.Run(() =>
            {
            // Create the raster layer on the active map
            layer = LayerFactory.Instance.CreateLayer(mapUri, oMap, LayerPosition.AutoArrange, displayName);

                // Set raster layer transparency and name
                if (layer != null)
                {
                    layer?.SetVisibility(bIsVisible);

                }
            });
            return BA_ReturnCode.Success;
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
                    rasterUri.LocalPath + " could not be found. Raster not displayed!!");
            }
        }

        public static async Task<BA_ReturnCode> DisplayLocationMapAsync(BA_Objects.Aoi oAoi)
        {
            string mapName = Constants.MAPS_AOI_LOCATION;
            string layoutName = Constants.MAPS_AOI_LOCATION_LAYOUT;
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Map map = null;
            Layout layout = null;
            bool bCreateMapPane = false;
            bool bCreateLayoutPane = false;
            await QueuedTask.Run(async () =>
            {
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
                    bCreateMapPane = true;
                }

                //Finding the first project item with name matches with layoutName
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

                // initialize layout pane
                bool bFoundIt = false;
                foreach (var pane in FrameworkApplication.Panes)
                {
                    if (!(pane is ILayoutPane layoutPane))  //if not a layout view, continue to the next pane    
                        continue;
                    if (layoutPane.LayoutView != null &&
                        layoutPane.LayoutView.Layout == layout) //if there is a match, activate the view  
                    {
                        bFoundIt = true;
                    }
                }
                if (!bFoundIt)
                {
                    bCreateLayoutPane = true;
                }

                success = await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_AOI_LOCATION_MAP_FRAME_NAME, layout, map,
                1.0, 2.0, 7.5, 9.0);

                // Remove existing layers from map frame
                string[] arrLayerNames = new string[2];
                arrLayerNames[0] = Constants.MAPS_AOI_BOUNDARY;
                arrLayerNames[1] = Constants.MAPS_WESTERN_STATES_BOUNDARY;
                foreach (string strName in arrLayerNames)
                {
                    Layer oLayer =
                        map.Layers.FirstOrDefault<Layer>(m => m.Name.Equals(strName, StringComparison.CurrentCultureIgnoreCase));
                    if (oLayer != null)
                    {
                        map.RemoveLayer(oLayer);
                    }
                }

                // add western state boundaries map service layer
                Webservices ws = new Webservices();
                string url = await ws.GetWesternStateBoundariesUriAsync();
                Uri uri = new Uri(url);                
                success = await MapTools.DisplayMapServiceLayerAsync(Constants.MAPS_AOI_LOCATION, uri, 
                        Constants.MAPS_WESTERN_STATES_BOUNDARY, true);
                if (success == BA_ReturnCode.Success)
                {
                    Module1.ActivateState("MapButtonPalette_BtnAoiLocation_State");
                }

                //add aoi boundary to map
                string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                 Constants.FILE_AOI_VECTOR;
                uri = new Uri(strPath);
                success = await MapTools.AddAoiBoundaryToMapAsync(uri, ColorFactory.Instance.RedRGB, Constants.MAPS_AOI_LOCATION, 
                    Constants.MAPS_AOI_BOUNDARY, true, 3);

                // create map elements
                success = await MapTools.AddMapElements(Constants.MAPS_AOI_LOCATION_MAP_FRAME_NAME, Constants.MAPS_AOI_LOCATION_LAYOUT, false);
                success = await MapTools.DisplayNorthArrowAsync(layout, Constants.MAPS_AOI_LOCATION_MAP_FRAME_NAME);
                success = await MapTools.DisplayScaleBarAsync(layout, Constants.MAPS_AOI_LOCATION_MAP_FRAME_NAME);

                GraphicElement textBox = layout.FindElement(Constants.MAPS_TITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = "LOCATION MAP";
                    textBox.SetGraphic(graphic);
                }

                textBox = layout.FindElement(Constants.MAPS_SUBTITLE) as GraphicElement;
                if (textBox != null)
                {
                    CIMTextGraphic graphic = (CIMTextGraphic)textBox.GetGraphic();
                    graphic.Text = oAoi.NwccName.ToUpper();
                    textBox.SetGraphic(graphic);
                }
            });

            success = await MapTools.DisplayLegendAsync(Constants.MAPS_AOI_LOCATION_MAP_FRAME_NAME, layout,
                "ArcGIS Colors", "1.5 Point", false);

            //Need to call on GUI thread
            if (bCreateMapPane)
            {
                IMapPane newMapPane = await ProApp.Panes.CreateMapPaneAsync(map);
            }
            if (bCreateLayoutPane)
            {
                ILayoutPane newLayoutPane = newLayoutPane = await ProApp.Panes.CreateLayoutPaneAsync(layout); //GUI thread
                //(newLayoutPane as Pane).Activate();
            }
            return success;
        }

        private static async Task<CIMMultipartColorRamp> CreateCustomColorRampAsync(int[,] colors)
        {
            var multiPartRamp = new CIMMultipartColorRamp
            {
                Weights = new double[colors.GetLength(0)]
            };
            await QueuedTask.Run(() =>
            {
                CIMColorRamp[] rampValues = new CIMColorRamp[colors.GetLength(0)];
                for (int i = 0; i < colors.GetLength(0) - 1; i++)
                {
                    var ramp = new CIMPolarContinuousColorRamp();
                    var r = colors[i, 0];
                    var g = colors[i, 1];
                    var b = colors[i, 2];
                    ramp.FromColor = new CIMRGBColor() { R = r, G = g, B = b };
                    r = colors[i + 1, 0];
                    g = colors[i + 1, 1];
                    b = colors[i + 1, 2];
                    ramp.ToColor = new CIMRGBColor() { R = r, G = g, B = b };
                    ramp.PolarDirection = PolarDirection.Clockwise;
                    rampValues[i] = ramp;
                    multiPartRamp.Weights[i] = 1;
                }
                multiPartRamp.Weights[colors.GetLength(0) - 1] = 1;

                multiPartRamp.ColorRamps = rampValues;
            });
            return multiPartRamp;
        }

        private static async Task<IList<BA_Objects.Interval>> CalculateSweZonesAsync(int idxDefaultMonth)
        {
            // Calculate interval list
            List<BA_Objects.Interval> lstIntervals = new List<BA_Objects.Interval>();
            int intZones = -1;
            if (Module1.Current.BatchToolSettings.SnotelSweZonesCount != null)
            {
                intZones = (int)Module1.Current.BatchToolSettings.SnotelSweZonesCount;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateSweZonesAsync),
                    "Unable to retrieve SnotelSweZonesCount from batch_tool_settings.json. Calculation halted!");
                return null;
            }
            intZones = intZones - 2;  //Subtract the 2 zones on the bottom that we create
            double[] arrReturnValues = await MapTools.SWEUnitsConversionAsync(Constants.DATA_TYPE_SWE, idxDefaultMonth);
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                Constants.FILES_SNODAS_SWE[idxDefaultMonth];
            string dataSourceUnits = await QuerySweLayerUnitsAsync(strPath);
            if (arrReturnValues.Length == 4)
            {
                bool bSkipInterval2 = false;
                double floor = 0.5;
                if (arrReturnValues[2] > floor)
                {
                    floor = arrReturnValues[2]; // min display value
                    bSkipInterval2 = true;
                }
                // Calculate elevation interval for remaining intervals
                double dblInterval = Math.Round(arrReturnValues[3] / intZones, 1);
                IList<BA_Objects.Interval> lstCalcInterval = new List<BA_Objects.Interval>();
                int zones = GeneralTools.CreateRangeArray(floor, arrReturnValues[3], dblInterval, out lstCalcInterval);
                // Make sure we don't have > than intzones
                if (zones > intZones)
                {
                    // Merge 2 upper zones
                    if (lstCalcInterval.Count >= intZones)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSweZonesAsync),
                            "Merging 2 upper intervals. Too many intervals created.");
                        var interval = lstCalcInterval[intZones - 1];
                        interval.UpperBound = lstCalcInterval[intZones].UpperBound;
                        lstCalcInterval.RemoveAt(intZones);
                    }
                }
                // Manually build first 2 intervals; Spec is hard-coded
                int idx = 1;
                BA_Objects.Interval interval1 = new BA_Objects.Interval
                {
                    LowerBound = 0.0F,
                    Value = idx
                };
                if (bSkipInterval2)
                {
                    interval1.UpperBound = floor;
                }
                else
                {
                    interval1.UpperBound = 0.0001;
                }
                interval1.Name = interval1.LowerBound + " - " + interval1.UpperBound.ToString("0." + new string('#', 10));
                lstIntervals.Add(interval1);
                idx++;
                if (!bSkipInterval2)
                {
                    BA_Objects.Interval interval2 = new BA_Objects.Interval
                    {
                        LowerBound = interval1.UpperBound,
                        UpperBound = floor,
                        Value = idx
                    };
                    lstIntervals.Add(interval2);
                    idx++;
                }
                // Reset values in calculated interval list
                foreach (var item in lstCalcInterval)
                {
                    item.Value = idx;
                    idx++;
                }
                // Reset lower bound for first item in lstCalcInterval
                var calcItem = lstCalcInterval[0];
                calcItem.LowerBound = lstIntervals.Last().UpperBound;
                // Merge 2 lists together
                lstIntervals.AddRange(lstCalcInterval);
                // Format name values
                foreach (var item in lstIntervals)
                {
                    if (item.Value == 1)    // first interval: 
                    {
                        item.Name = String.Format("{0:0}", item.LowerBound);
                    }
                    else if (item.Value == 2)   // second interval
                    {
                        item.Name = String.Format("{0:0}", item.LowerBound) + " - " +
                            String.Format("{0:0.0}", item.UpperBound);
                    }
                    else
                    {
                        item.Name = String.Format("{0:0.0}", item.LowerBound) + " - " +
                            String.Format("{0:0.0}", item.UpperBound);
                    }
                }
                // Reset lower and upper bound to layer units
                string strDisplayUnits = Module1.Current.BatchToolSettings.SweDisplayUnits;
                if (!dataSourceUnits.Equals(strDisplayUnits))
                {
                    foreach (var nextInterval in lstIntervals)
                    {
                        if (strDisplayUnits.Equals(Constants.UNITS_INCHES))
                        {
                            nextInterval.LowerBound = LinearUnit.Inches.ConvertTo(nextInterval.LowerBound, LinearUnit.Millimeters);
                            nextInterval.UpperBound = LinearUnit.Inches.ConvertTo(nextInterval.UpperBound, LinearUnit.Millimeters);
                        }
                        else if (dataSourceUnits.Equals(Constants.UNITS_MILLIMETERS))
                        {
                            nextInterval.LowerBound = LinearUnit.Millimeters.ConvertTo(nextInterval.LowerBound, LinearUnit.Inches);
                            nextInterval.UpperBound = LinearUnit.Millimeters.ConvertTo(nextInterval.UpperBound, LinearUnit.Inches);
                        }
                    }
                }
                return lstIntervals;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateSweZonesAsync),
                    "Unable to retrieve min/max SWE values from analysis.xml. Calculation halted!");
                return null;
            }
        }

        private static async Task<IList<BA_Objects.Interval>> CalculateSweDeltaZonesAsync(int idxDefaultMonth)
        {
            // Calculate interval list
            int intZones = 7;
            intZones = intZones - 1;  //Subtract the zones in the middle that we create
            int halfZones = intZones / 2;
            double[] arrReturnValues = await MapTools.SWEUnitsConversionAsync(Constants.DATA_TYPE_SWE_DELTA, idxDefaultMonth);
            string strPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +
                Constants.FILES_SNODAS_SWE[idxDefaultMonth];
            string dataSourceUnits = await QuerySweLayerUnitsAsync(strPath);    // Data source units come from source SWE layer
            if (arrReturnValues.Length == 4)
            {
                // Calculate interval list for negative values
                double dblInterval = Math.Round(arrReturnValues[2] / halfZones, 2);
                //determine the interval decimal place to add an increment value to the lower bound
                IList<BA_Objects.Interval> lstNegInterval = new List<BA_Objects.Interval>();
                int zones = GeneralTools.CreateRangeArray(arrReturnValues[2], -0.00001, Math.Abs(dblInterval), out lstNegInterval);
                // Make sure we don't have > than intzones / 2
                if (zones > halfZones)
                {
                    // Merge 2 lower zones
                    if (lstNegInterval.Count > halfZones)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSweDeltaZonesAsync),
                            "Merging 2 lowest intervals. Too many intervals created.");
                        var interval = lstNegInterval[0];
                        interval.LowerBound = lstNegInterval[1].LowerBound;
                        lstNegInterval.RemoveAt(0);
                    }
                }
                // Reset upper interval to mesh with middle interval
                lstNegInterval[halfZones - 1].UpperBound = -0.00001;
                // Manually build middle intervals; Spec is defined
                BA_Objects.Interval oInterval = new BA_Objects.Interval
                {
                    LowerBound = lstNegInterval[halfZones-1].UpperBound,
                    UpperBound = 0.00001F,
                    Value = halfZones + 1
                };
                lstNegInterval.Add(oInterval);

                // Calculate interval list for negative values
                dblInterval = Math.Round(arrReturnValues[3] / halfZones, 2);
                IList<BA_Objects.Interval> lstPosInterval = new List<BA_Objects.Interval>();
                zones = GeneralTools.CreateRangeArray(lstNegInterval.Last().UpperBound, arrReturnValues[3],dblInterval, out lstPosInterval);
                // Make sure we don't have > than half zones
                if (zones > halfZones)
                {
                    // Merge 2 upper zones
                    if (lstPosInterval.Count > halfZones)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSweDeltaZonesAsync),
                            "Merging 2 highest intervals. Too many intervals created.");
                        var interval = lstPosInterval[zones - 1];
                        interval.UpperBound = lstPosInterval[halfZones].UpperBound;
                        lstPosInterval.RemoveAt(halfZones);
                    }
                }
                // Reset lower interval to mesh with middle interval
                lstPosInterval[0].LowerBound = lstNegInterval.Last().UpperBound;

                // Merge intervals to create 1 list
                foreach (var item in lstPosInterval)
                {
                    lstNegInterval.Add(item);
                }

                // Reset values in calculated interval list
                int idx = 1;
                foreach (var item in lstNegInterval)
                {
                    item.Value = idx;
                    // Format name property
                    if (idx == halfZones + 1) // Middle interval
                    {
                        item.Name = String.Format("{0:0}", item.LowerBound);
                    }
                    else if (idx == halfZones)
                    {
                        item.Name = String.Format("{0:0.00}", item.LowerBound) + " - " +
                            String.Format("{0:0}", item.UpperBound);
                    }
                    else if (idx == halfZones + 2)
                    {
                        item.Name = String.Format("{0:0}", item.LowerBound) + " - " +
                            String.Format("{0:0.00}", item.UpperBound);
                    }
                    else
                    {
                        item.Name = String.Format("{0:0.00}", item.LowerBound) + " - " +
                            String.Format("{0:0.00}", item.UpperBound);
                    }
                    idx++;
                }
                string strDisplayUnits = Module1.Current.BatchToolSettings.SweDisplayUnits;
                if (!dataSourceUnits.Equals(strDisplayUnits))
                {
                    foreach (var nextInterval in lstNegInterval)
                    {
                        if (strDisplayUnits.Equals(Constants.UNITS_INCHES))
                        {
                            nextInterval.LowerBound = LinearUnit.Inches.ConvertTo(nextInterval.LowerBound, LinearUnit.Millimeters);
                            nextInterval.UpperBound = LinearUnit.Inches.ConvertTo(nextInterval.UpperBound, LinearUnit.Millimeters);
                        }
                        else if (dataSourceUnits.Equals(Constants.UNITS_MILLIMETERS))
                        {
                            nextInterval.LowerBound = LinearUnit.Millimeters.ConvertTo(nextInterval.LowerBound, LinearUnit.Inches);
                            nextInterval.UpperBound = LinearUnit.Millimeters.ConvertTo(nextInterval.UpperBound, LinearUnit.Inches);
                        }
                    }
                }
                return lstNegInterval;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateSweDeltaZonesAsync),
                    "Unable to retrieve min/max SWE values from analysis.xml. Calculation halted!");
                return null;
            }
        }

        private static IList<BA_Objects.Interval> CalculateSeasonalPrecipZones()
        {
            // Calculate interval list
            int intZones = 7;
            intZones = intZones - 1;  //Subtract the zones in the middle that we create
            int halfZones = intZones / 2;
            BA_Objects.Analysis oAnalysis = GeneralTools.GetAnalysisSettings(Module1.Current.Aoi.FilePath);
            if (oAnalysis != null && oAnalysis.SeasonalPrecipMin > 0 && oAnalysis.SeasonalPrecipMax > 0)
            {
                // Calculate interval list for lower-range values
                double lBound = 23.0F;
                double uBound = 27.0F;
                IList<BA_Objects.Interval> lstNegInterval = new List<BA_Objects.Interval>();
                double dblRange = -1.0F;
                double dblInterval = -1.0F;
                int zones = -1;
                if (oAnalysis.SeasonalPrecipMin >= lBound)
                {
                    lBound = oAnalysis.SeasonalPrecipMin;
                    // Manually build middle intervals; Spec is defined
                    BA_Objects.Interval oInterval = new BA_Objects.Interval
                    {
                        LowerBound = lBound,
                        UpperBound = uBound,
                        Value = 1
                    };
                    lstNegInterval.Add(oInterval);
                }
                else
                {
                    dblRange = lBound - oAnalysis.SeasonalPrecipMin;
                    dblInterval = Math.Round(dblRange / halfZones, 2);
                    //determine the interval decimal place to add an increment value to the lower bound
                    zones = GeneralTools.CreateRangeArray(oAnalysis.SeasonalPrecipMin, lBound, dblInterval, out lstNegInterval);
                    // Make sure we don't have > than intzones / 2
                    if (zones > halfZones)
                    {
                        // Merge 2 lower zones
                        if (lstNegInterval.Count > halfZones)
                        {
                            Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSweDeltaZonesAsync),
                                "Merging 2 lowest intervals. Too many intervals created.");
                            var interval = lstNegInterval[0];
                            interval.LowerBound = lstNegInterval[1].LowerBound;
                            lstNegInterval.RemoveAt(0);
                        }
                    }
                    // Reset upper interval to mesh with middle interval
                    lstNegInterval[halfZones - 1].UpperBound = lBound;
                    // Manually build middle intervals; Spec is defined
                    BA_Objects.Interval oInterval = new BA_Objects.Interval
                    {
                        LowerBound = lstNegInterval[halfZones - 1].UpperBound,
                        UpperBound = uBound,
                        Value = halfZones + 1
                    };
                    lstNegInterval.Add(oInterval);
                }

                // Calculate interval list for positive values
                dblRange = oAnalysis.SeasonalPrecipMax - uBound;
                dblInterval = Math.Round(dblRange / halfZones, 2);
                IList<BA_Objects.Interval> lstPosInterval = new List<BA_Objects.Interval>();
                zones = GeneralTools.CreateRangeArray(uBound, oAnalysis.SeasonalPrecipMax, dblInterval, out lstPosInterval);
                // Make sure we don't have > than half zones
                if (zones > halfZones)
                {
                    // Merge 2 upper zones
                    if (lstPosInterval.Count > halfZones)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(CalculateSweDeltaZonesAsync),
                            "Merging 2 highest intervals. Too many intervals created.");
                        var interval = lstPosInterval[zones - 1];
                        interval.UpperBound = lstPosInterval[halfZones].UpperBound;
                        lstPosInterval.RemoveAt(halfZones);
                    }
                }
                // Reset lower interval to mesh with middle interval
                lstPosInterval[0].LowerBound = lstNegInterval.Last().UpperBound;

                // Merge intervals to create 1 list
                foreach (var item in lstPosInterval)
                {
                    lstNegInterval.Add(item);
                }

                // Reset values in calculated interval list
                int idx = 1;
                foreach (var item in lstNegInterval)
                {
                    item.Value = idx;
                    // Format name property
                    item.Name = String.Format("{0:0.0}", item.LowerBound) + " - " +
                            String.Format("{0:0.0}", item.UpperBound);
                    idx++;
                }
                return lstNegInterval;
            }
            else
            {
                Module1.Current.ModuleLogManager.LogError(nameof(CalculateSweDeltaZonesAsync),
                    "Unable to retrieve min/max seasonal precip values from analysis.xml. Calculation halted!");
                return null;
            }
        }
    }
}
