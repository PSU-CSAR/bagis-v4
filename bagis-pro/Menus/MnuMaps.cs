﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace bagis_pro.Menus
{
    internal class MnuMaps_BtnSelectAoi : Button
    {
        protected async override void OnClick()
        {
            try
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
                        FolderType fType = await GeodatabaseTools.GetAoiFolderTypeAsync(selectedItem.Path);
                        if (fType != FolderType.AOI)
                        {
                            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("!!The selected folder does not contain a valid AOI", "BAGIS Pro");
                        }
                        else
                        {
                            // Initialize AOI object
                            BA_Objects.Aoi oAoi = new BA_Objects.Aoi(System.IO.Path.GetFileName(selectedItem.Path), selectedItem.Path);
                            // Store current AOI in Module1
                            Module1.Current.Aoi = oAoi;
                            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("AOI is set to " + oAoi.Name + "!", "BAGIS PRO");
                        }

                    }

                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to set the AOI!! " + e.Message, "BAGIS PRO");
            }

        }
    }

    internal class MnuMaps_BtnMapTest : Button
    {
        protected async override void OnClick()
        {
            string tempAoiPath = "C:\\Docs\\animas_AOI_prms";
            try
            {
                await MapTools.DisplayMaps(tempAoiPath);
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to load the maps!! " + e.Message, "BAGIS PRO");
            }
        }
    }

}
