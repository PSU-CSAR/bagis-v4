﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;


namespace bagis_pro
{
    internal class DockMigrateToFGDBViewModel : DockPane
    {
        private const string _dockPaneID = "bagis_pro_DockMigrateToFGDB";

        protected DockMigrateToFGDBViewModel()
        {
            Names = new ObservableCollection<BA_Objects.Aoi>();
            Names.CollectionChanged += NamesCollectionChanged;
        }


        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Migrate to File Geodatabase";
        private string _aoiFolder = "";
        private bool _canRun = false;   // Flag that indicates if things are in a state that the process can successfully run; Enables the button on the form
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }
        public string AoiFolder
        {
            get { return _aoiFolder; }
            set
            {
                SetProperty(ref _aoiFolder, value, () => AoiFolder);
            }
        }

        public ObservableCollection<BA_Objects.Aoi> Names { get; set; }

        public void NamesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Allows us to respond when properties of items in the collection change; ie: including the AOI in the list to migrate
            if (e.OldItems != null)
                foreach (BA_Objects.Aoi oldItem in e.OldItems)
                    oldItem.PropertyChanged -= NamesCollection_PropertyChanged;

            if (e.NewItems != null)
                foreach (BA_Objects.Aoi newItem in e.NewItems)
                    newItem.PropertyChanged += NamesCollection_PropertyChanged;
        }

        private void NamesCollection_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            ManageRunButton();
        }

        void ManageRunButton()
        {
            //This will get called when an item in the collection is changed to manage the run button
            int validAoiCount = 0;
            foreach (var item in Names)
            {
                if (item.AoiBatchIsSelected == true)
                {
                    validAoiCount++;
                }
            }
            if (validAoiCount > 0)
            {
                _canRun = true;
            }
            else
            {
                _canRun = false;
            }
        }

        public ICommand CmdAoiFolder
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    //Display the filter in an Open Item dialog
                    OpenItemDialog aNewFilter = new OpenItemDialog
                    {
                        Title = "Select a basin, aoi, or folder",
                        MultiSelect = false,
                        Filter = ItemFilters.folders
                    };
                    bool? ok = aNewFilter.ShowDialog();
                    bool bOk = ok ?? false;
                    if (bOk)
                    {
                        AoiFolder = "";
                        var arrFileNames = aNewFilter.Items;
                        foreach (var item in arrFileNames)
                        {
                            AoiFolder = item.Path;
                        }
                    }

                    string strLogFolder = AoiFolder + "\\" + Constants.FOLDER_MAP_PACKAGE;
                    // Make sure the maps_publish folder exists under the selected folder
                    if (!Directory.Exists(Path.GetDirectoryName(strLogFolder)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(strLogFolder));
                    }

                    // Set logger to parent folder directory
                    Module1.Current.ModuleLogManager.UpdateLogFileLocation(strLogFolder);

                    Names.Clear();
                    try
                    {
                        string[] folders = Directory.GetDirectories(AoiFolder, "*", SearchOption.AllDirectories);
                        foreach (var item in folders)
                        {
                            FolderType fType = await GeodatabaseTools.GetWeaselAoiFolderTypeAsync(item);
                            if (fType == FolderType.FOLDER)
                            {
                                // Do nothing; skip
                            }
                            else
                            {
                                IDictionary<string, string> reqRasterDict = GetDictOfReqRasters(item);
                                IList<string> existsLayersList = await GeneralTools.RasterDatasetsExistAsync(reqRasterDict.Keys);
                                IList<string> missingReqLayersList = new List<string>();
                                if (existsLayersList.Count < reqRasterDict.Keys.Count)
                                {
                                    foreach (var aLayer in reqRasterDict.Keys)
                                    {
                                        if (!existsLayersList.Contains(aLayer))
                                        {
                                            missingReqLayersList.Add(aLayer);
                                        }
                                    }
                                }
                                // Accomodate two possible names for raster aoi boundary layer (aoibagis or aoi)
                                string aoiGdb = GeodatabaseTools.GetGeodatabasePath(item, GeodatabaseNames.Aoi, true);
                                IList<string> lstTest = new List<string>
                                {
                                    item + @"\aoibagis"
                                };
                                existsLayersList = await GeneralTools.RasterDatasetsExistAsync(lstTest);
                                if (existsLayersList.Count == 0)
                                {
                                    lstTest.Clear();
                                    string strLayer = item + @"\aoi";
                                    lstTest.Add(strLayer);
                                    existsLayersList = await GeneralTools.RasterDatasetsExistAsync(lstTest);
                                    reqRasterDict[strLayer] = aoiGdb + Constants.FILE_AOI_RASTER;
                                    if (existsLayersList.Count == 0)
                                    {
                                        missingReqLayersList.Add(item + @"\aoibagis");
                                    }
                                }
                                else
                                {
                                    reqRasterDict[item + @"\aoibagis"] = aoiGdb + Constants.FILE_AOI_RASTER;
                                }
                                IDictionary<string, string> reqVectorDict = GetDictOfReqWeaselVectors(item);
                                existsLayersList = await GeneralTools.ShapefilesExistAsync(reqVectorDict.Keys);
                                if (reqVectorDict.Keys.Count > existsLayersList.Count)
                                {
                                    foreach (var aLayer in reqVectorDict.Keys)
                                    {
                                        if (!existsLayersList.Contains(aLayer))
                                        {
                                            missingReqLayersList.Add(aLayer);
                                        }
                                    }
                                }

                                if (missingReqLayersList.Count > 0)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.Append(item + " cannot be exported to File Geodatabase format. ");
                                    sb.Append("The data layers listed below are missing. These files ");
                                    sb.Append("must be present before attempting the conversion:\r\n");
                                    foreach (var missing in missingReqLayersList)
                                    {
                                        sb.Append(missing + "\r\n");
                                    }
                                    Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), sb.ToString());
                                }
                                else
                                {
                                    IList<string> missingOptLayersList = new List<string>();
                                    IDictionary<string, string> optRasterDict = GetDictOptWeaselRasters(item, fType);
                                    IList<string> existsOptLayersList = await GeneralTools.RasterDatasetsExistAsync(optRasterDict.Keys);
                                    if (existsOptLayersList.Count < optRasterDict.Keys.Count)
                                    {
                                        foreach (var aLayer in optRasterDict.Keys)
                                        {
                                            if (!existsOptLayersList.Contains(aLayer))
                                            {
                                                missingOptLayersList.Add(aLayer);
                                            }
                                        }
                                    }
                                    lstTest.Clear();
                                    lstTest.Add(item + @"\unsnappedpp.shp");
                                    existsOptLayersList = await GeneralTools.ShapefilesExistAsync(lstTest);
                                    if (existsLayersList.Count == 0)
                                    {
                                        missingOptLayersList.Add(item + @"\unsnappedpp.shp");
                                    }
                                    if (missingOptLayersList.Count > 0)
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.Append("The following files normally present in a Weasel AOI are ");
                                        sb.Append("missing from " + item + " and will not be converted: \r\n");
                                        foreach (var missing in missingOptLayersList)
                                        {
                                            sb.Append(missing + "\r\n");
                                        }
                                        Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), sb.ToString());
                                    }

                                    BA_Objects.Aoi aoi = new BA_Objects.Aoi(Path.GetFileName(item), item);
                                    IList<string> lstExistingGdb = CheckForBagisGdb(item);
                                    if (lstExistingGdb.Count > 0)
                                    {
                                        StringBuilder sb = new StringBuilder();
                                        sb.Append("At least one geodatabase already exists in aoi " + aoi.Name);
                                        sb.Append(". \r\n");
                                        sb.Append("Do you wish to overwrite them? All existing data will be lost!\r\n");
                                        System.Windows.MessageBoxResult res = MessageBox.Show(sb.ToString(), "BAGIS-PRO",
                                            System.Windows.MessageBoxButton.YesNo);
                                        if (res != System.Windows.MessageBoxResult.Yes)
                                        {
                                            aoi.AoiBatchIsSelected = false;
                                        }
                                    }
                                    Names.Add(aoi);
                                    ManageRunButton();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        string strLogEntry = "An error occurred while interrogating the subdirectories " + e.StackTrace + "\r\n";
                        Module1.Current.ModuleLogManager.LogError(nameof(CmdAoiFolder), strLogEntry);
                    }
                });
            }
        }


        private RelayCommand _runCommand;
        public ICommand CmdRun
        {
            get
            {
                if (_runCommand == null)
                    _runCommand = new RelayCommand(RunImplAsync, () => _canRun);
                return _runCommand;
            }
        }

        // Maps the weasel files to their fgdb locations; key is weasel path and value is fgdb path
        IDictionary<string, string> GetDictOfReqRasters(string aoiPath)
        {
            IDictionary<string, string> dictRasters = new Dictionary<string, string>();
            // surfaces layers
            string surfacesGdb = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true);
            string weaselPath = aoiPath + @"\output\surfaces\dem\filled\grid";   // Filled DEM
            string gdbPath = surfacesGdb + Constants.FILE_DEM_FILLED;
            dictRasters[weaselPath] = gdbPath;
            weaselPath = aoiPath + @"\output\surfaces\dem\filled\aspect\grid";   // Aspect
            gdbPath = surfacesGdb + Constants.FILE_ASPECT;
            dictRasters[weaselPath] = gdbPath;
            weaselPath = aoiPath + @"\output\surfaces\dem\filled\slope\grid";   // Slope
            gdbPath = surfacesGdb + Constants.FILE_SLOPE;
            dictRasters[weaselPath] = gdbPath;
            weaselPath = aoiPath + @"\output\surfaces\dem\filled\flow-direction\flow-accumulation\grid";   // Flow accumulation
            gdbPath = surfacesGdb + Constants.FILE_FLOW_ACCUMULATION;
            dictRasters[weaselPath] = gdbPath;
            weaselPath = aoiPath + @"\output\surfaces\dem\filled\flow-direction\grid";   // Flow direction
            gdbPath = surfacesGdb + Constants.FILE_FLOW_DIRECTION;
            dictRasters[weaselPath] = gdbPath;

            // A couple of aoi layers
            string aoiGdb = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi, true);
            weaselPath = aoiPath + @"\aoib";
            gdbPath = aoiGdb + Constants.FILE_AOI_BUFFERED_RASTER;
            dictRasters[weaselPath] = gdbPath;
            weaselPath = aoiPath + @"\p_aoi";
            gdbPath = aoiGdb + Constants.FILE_AOI_PRISM_RASTER;
            dictRasters[weaselPath] = gdbPath;
            return dictRasters;
        }

        IDictionary<string, string> GetDictOfReqWeaselVectors(string aoiPath)
        {
            IDictionary<string, string> dictVectors = new Dictionary<string, string>();
            string aoiGdb = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Aoi, true);
            string layerPath = aoiPath + @"\aoi_v.shp";
            string gdbPath = aoiGdb + Constants.FILE_AOI_VECTOR;
            dictVectors[layerPath] = gdbPath;
            layerPath = aoiPath + @"\aoib_v.shp";
            gdbPath = aoiGdb + Constants.FILE_AOI_BUFFERED_VECTOR;
            dictVectors[layerPath] = gdbPath;
            layerPath = aoiPath + @"\p_aoi_v.shp";
            gdbPath = aoiGdb + Constants.FILE_AOI_PRISM_VECTOR;
            dictVectors[layerPath] = gdbPath;
            layerPath = aoiPath + @"\pourpoint.shp";
            gdbPath = aoiGdb + Constants.FILE_POURPOINT;
            dictVectors[layerPath] = gdbPath;
            return dictVectors;
        }

        IDictionary<string, string> GetDictOptWeaselRasters(string aoiPath, FolderType fType)
        {
            IDictionary<string, string> dictRasters = new Dictionary<string, string>();
            string surfacesGdb = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true);
            string layerPath = aoiPath + @"\output\surfaces\dem\grid";
            string gdbPath = surfacesGdb + Constants.FILE_DEM;
            dictRasters[layerPath] = gdbPath;
            layerPath = aoiPath + @"\output\surfaces\dem\filled\hillshade\grid";
            gdbPath = surfacesGdb + Constants.FILE_HILLSHADE;
            dictRasters[layerPath] = gdbPath;

            if (fType == FolderType.AOI)
            {
                // prism layers; optional because they can be reclipped
                string[] arrPrismLayers = {"jan","feb","mar","apr","may","jun","jul","aug","sep",
                                       "oct","nov","dec","q1","q2","q3","q4","annual" };
                string prismGdb = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Prism, true);
                int i = 0;
                foreach (var month in arrPrismLayers)
                {
                    layerPath = aoiPath + @"\layers\PRISM\" + month + @"\grid";
                    PrismFile nextMonth = (PrismFile)i;
                    gdbPath = prismGdb + nextMonth.ToString();
                    dictRasters[layerPath] = gdbPath;
                    i++;
                }
            }
            return dictRasters;
        }

        IList<string> CheckForBagisGdb(string aoiPath)
        {
            IList<string> lstBagisGdb = new List<string>();
            foreach (var strName in GeodatabaseNames.AllNames)
            {
                if (Directory.Exists(aoiPath + "\\" + strName))
                {
                    lstBagisGdb.Add(aoiPath + "\\" + strName);
                }
            }
            return lstBagisGdb;
        }

        private async void RunImplAsync(object param)
        {
            // Bring GP History tool forward
            var cmdShowHistory = FrameworkApplication.GetPlugInWrapper("esri_geoprocessing_showToolHistory") as ICommand;
            if (cmdShowHistory != null)
            {
                if (cmdShowHistory.CanExecute(null))
                {
                    cmdShowHistory.Execute(null);
                }
            }
            foreach (var oAoi in Names)
            {
                if (oAoi.AoiBatchIsSelected)
                {
                    IList<string> lstExistingGdb = CheckForBagisGdb(oAoi.FilePath);

                    // Make directory for log if it doesn't exist
                    if (!Directory.Exists(oAoi.FilePath + "\\" + Constants.FOLDER_LOGS))
                    {
                        DirectoryInfo info = Directory.CreateDirectory(oAoi.FilePath + "\\" + Constants.FOLDER_LOGS);
                        if (info == null)
                        {
                            MessageBox.Show("Unable to create logs directory in Aoi folder!!", "BAGIS-PRO");
                        }
                    }
                    // Set logger to AOI directory
                    string logFolderName = oAoi.FilePath + "\\" + Constants.FOLDER_LOGS;
                    Module1.Current.ModuleLogManager.UpdateLogFileLocation(logFolderName);

                    // Delete old geodatabases if they exist
                    foreach (var geodatabasePath in lstExistingGdb)
                    {
                        IGPResult gpResult = await QueuedTask.Run(() =>
                        {
                            var parameters = Geoprocessing.MakeValueArray(geodatabasePath);
                            return Geoprocessing.ExecuteToolAsync("Delete_management", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        });
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync), 
                                "Unable to delete geodatabase. Error code: " + gpResult.ErrorCode);
                            MessageBox.Show("Unable to delete geodatabase " + geodatabasePath + "!");
                        }
                    }

                    // Create new geodatabases
                    BA_ReturnCode success = await GeodatabaseTools.CreateGeodatabaseFoldersAsync(oAoi.FilePath);
                    if (success == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogInfo(nameof(RunImplAsync),
                            "Created geodatabases in " + oAoi.FilePath);
                    }
                    else
                    {
                        MessageBox.Show("Unable to create geodatabases in " + oAoi.FilePath + ". Check logs!");
                    }

                    // Assemble a dictionary with rasters we want to copy
                    IDictionary<string, string> rastersToCopy = GetDictOfReqRasters(oAoi.FilePath);
                    // Accomodate two possible names for raster aoi boundary layer (aoibagis or aoi)
                    string aoiGdb = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true);
                    IList<string> lstTest = new List<string>
                    {
                        oAoi.FilePath + @"\aoibagis"
                    };
                    IList<string> existingLayers = await GeneralTools.RasterDatasetsExistAsync(lstTest);
                    if (existingLayers.Count == 0)
                    {
                        lstTest.Clear();
                        string strLayer = oAoi.FilePath + @"\aoi";
                        lstTest.Add(strLayer);
                        existingLayers = await GeneralTools.RasterDatasetsExistAsync(lstTest);
                        if (existingLayers.Count > 0)
                        {
                            rastersToCopy[strLayer] = aoiGdb + Constants.FILE_AOI_RASTER;
                        }
                    }
                    else
                    {
                        rastersToCopy[oAoi.FilePath + @"\aoibagis"] = aoiGdb + Constants.FILE_AOI_RASTER;
                    }
                    // Check to see if optional layers are present
                    FolderType fType = await GeodatabaseTools.GetWeaselAoiFolderTypeAsync(oAoi.FilePath);
                    IDictionary<string, string> optRasterDict = GetDictOptWeaselRasters(oAoi.FilePath, fType);
                    existingLayers = await GeneralTools.RasterDatasetsExistAsync(optRasterDict.Keys);
                    foreach (var layerPath in existingLayers)
                    {
                        string gdbPath = optRasterDict[layerPath];
                        rastersToCopy[layerPath] = gdbPath;
                    }
                    // Raster layers with non-deterministic names in analysis and layers folders
                    string strWeaselFolder = oAoi.FilePath + @"\layers";
                    string strGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true);
                    IList<string> lstRasters = await GeneralTools.GetLayersInFolderAsync(strWeaselFolder, "Raster Dataset");
                    foreach (var item in lstRasters)
                    {
                        rastersToCopy[strWeaselFolder + "\\" + item] = strGdbPath + item;
                    }
                    strWeaselFolder = oAoi.FilePath + @"\analysis";
                    strGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true);
                    lstRasters = await GeneralTools.GetLayersInFolderAsync(strWeaselFolder, "Raster Dataset");
                    foreach (var item in lstRasters)
                    {
                        rastersToCopy[strWeaselFolder + "\\" + item] = strGdbPath + item;
                    }

                    // Use Geoprocessor to copy the files
                    int errorCount = 0;
                    foreach (var key in rastersToCopy.Keys)
                    {
                        IGPResult gpResult = await QueuedTask.Run(() =>
                        {
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath, cellSize: "MINOF");
                            var parameters = Geoprocessing.MakeValueArray(key, rastersToCopy[key]);
                            return Geoprocessing.ExecuteToolAsync("CopyRaster_management", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        });
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                "Failed to copy raster " + key + "!");
                            errorCount++;
                        }
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync),
                        "Raster copy completed with " + errorCount + " errors.");

                    // Assemble a dictionary with vectors we want to copy
                    IDictionary<string, string> vectorsToCopy = GetDictOfReqWeaselVectors(oAoi.FilePath);
                    // Check for an optional vector
                    lstTest.Clear();
                    lstTest.Add(oAoi.FilePath + @"\unsnappedpp.shp");
                    existingLayers = await GeneralTools.ShapefilesExistAsync(lstTest);
                    if (existingLayers.Count > 0)
                    {
                        vectorsToCopy[oAoi.FilePath + @"\unsnappedpp.shp"] = aoiGdb + Constants.FILE_UNSNAPPED_POURPOINT;
                    }

                    // Vector layers with non-deterministic names in analysis and layers folders
                    strWeaselFolder = oAoi.FilePath + @"\layers";
                    strGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Layers, true);
                    IList<string> lstVectors = await GeneralTools.GetLayersInFolderAsync(strWeaselFolder, "Shapefile");
                    foreach (var item in lstVectors)
                    {
                        string noExtension = Path.GetFileNameWithoutExtension(item);
                        vectorsToCopy[strWeaselFolder + "\\" + item] = strGdbPath + noExtension;
                    }
                    strWeaselFolder = oAoi.FilePath + @"\analysis";
                    strGdbPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Analysis, true);
                    lstVectors = await GeneralTools.GetLayersInFolderAsync(strWeaselFolder, "Shapefile");
                    foreach (var item in lstVectors)
                    {
                        string noExtension = Path.GetFileNameWithoutExtension(item);
                        vectorsToCopy[strWeaselFolder + "\\" + item] = strGdbPath + noExtension;
                    }

                    // Use Geoprocessor to copy the files
                    errorCount = 0;
                    foreach (var entry in vectorsToCopy)
                    {
                        string strKey = entry.Key;
                    }
                    string strTempFile = Path.GetFileName("tmpVector");
                    string strDirectory = "";
                    foreach (var entry in vectorsToCopy)
                    {
                        IGPResult gpResult = await QueuedTask.Run(() =>
                        {
                            var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                            strDirectory = Path.GetDirectoryName(entry.Value);
                            var parameters = Geoprocessing.MakeValueArray(entry.Key, strDirectory, strTempFile);
                            return Geoprocessing.ExecuteToolAsync("FeatureClassToFeatureClass_conversion", parameters, null,
                                        CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                        });
                        if (gpResult.IsFailed)
                        {
                            Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                "Failed to convert vector " + entry.Key + "!");
                            errorCount++;
                        }
                        else
                        {
                            //There is a bug with using converted shapefiles in Pro; We will make a copy of the converted 
                            //file and then ultimately delete the final temp file
                            gpResult = await QueuedTask.Run(() =>
                            {
                                var environments = Geoprocessing.MakeEnvironmentArray(workspace: oAoi.FilePath);
                                strDirectory = Path.GetDirectoryName(entry.Value);
                                var parameters = Geoprocessing.MakeValueArray(strDirectory + "\\" + strTempFile, entry.Value);
                                return Geoprocessing.ExecuteToolAsync("CopyFeatures_management", parameters, null,
                                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                            });
                            if (gpResult.IsFailed)
                            {
                                Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                                    "Failed to copy feature class " + entry.Key + "!");
                                errorCount++;
                            }
                        }
                    }
                    Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync),
                        "Vector copy completed with " + errorCount + " errors.");
                    // Delete temp files
                    success = await GeoprocessingTools.DeleteDatasetAsync(strDirectory + "\\" + strTempFile);
                    if (success == BA_ReturnCode.Success)
                    {
                        Module1.Current.ModuleLogManager.LogDebug(nameof(RunImplAsync),
                            "Deleted temporary vector file " + strDirectory + "\\" + strTempFile);
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(RunImplAsync),
                            "Unable to delete temporary vector file " + strDirectory + "\\" + strTempFile);
                    }
                }
            }

            MessageBox.Show("Done!");
        }

}

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DockMigrateToFGDB_ShowButton : Button
    {
        protected override void OnClick()
        {
            DockMigrateToFGDBViewModel.Show();
        }
    }
}
