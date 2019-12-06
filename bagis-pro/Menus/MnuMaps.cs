﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace bagis_pro.Menus
{
    internal class MnuMaps_BtnSelectAoi : Button
    {
        protected async override void OnClick()
        {
            OpenItemDialog selectAoiDialog = new OpenItemDialog()
            {
                Title = "Select AOI Folder",
                InitialLocation = System.IO.Directory.GetCurrentDirectory(),
                MultiSelect = false,
                Filter = ItemFilters.folders
            };
            bool? boolOk = selectAoiDialog.ShowDialog();
            if (boolOk == true)
            {
                IEnumerable<Item> selectedItems = selectAoiDialog.Items;
                foreach (Item selectedItem in selectedItems)    // there will only be one
                {
                    FolderType fType = await GeodatabaseTools.GetAoiFolderType(selectedItem.Path);
                    if (fType != FolderType.AOI)
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("!!The selected folder does not contain a valid AOI","BAGIS Pro");
                    }
                    else
                    {
                        // Initialize AOI object
                        BA_Objects.Aoi oAoi = new BA_Objects.Aoi(System.IO.Path.GetFileName(selectedItem.Path), selectedItem.Path);
                        // Store current AOI in Module1
                        Module1.Current.Aoi = oAoi;
                    }

                }

            }

        }
    }

    internal class MnuMaps_BtnMapTest : Button
    {
        protected async override void OnClick()
        {
            string tempAoiPath = "C:\\Docs\\animas_AOI_prms";
            BA_Objects.Aoi oAoi = Module1.Current.Aoi;
            if (String.IsNullOrEmpty(oAoi.Name))
            {
                if (System.IO.Directory.Exists(tempAoiPath))
                {
                    // Initialize AOI object
                    oAoi = new BA_Objects.Aoi("animas_AOI_prms", tempAoiPath);
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
                    ILayoutPane iNewLayoutPane = await ProApp.Panes.CreateLayoutPaneAsync(layout); //GUI thread
                    await MapTools.SetDefaultMapFrameDimensionAsync(Constants.MAPS_DEFAULT_MAP_FRAME_NAME, layout, oMap,
                        1.0, 2.0, 7.5, 9.0);

                    //add aoi boundary to map and zoom to layer
                    string strPath = GeodatabaseTools.GetGeodatabasePath(oAoi.FilePath, GeodatabaseNames.Aoi, true) +
                                     "aoi_v";
                    Uri uri = new Uri(strPath);
                    await MapTools.AddAoiBoundaryToMapAsync(uri, Constants.MAPS_AOI_BOUNDARY);
                    
                    //zoom to aoi boundary layer
                    double bufferFactor = 1.1;
                    bool bZoomed = await MapTools.ZoomToExtentAsync(uri, bufferFactor);
                }
            }
        }
    }

}
