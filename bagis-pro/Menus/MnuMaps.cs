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
using ArcGIS.Desktop.Mapping;

namespace bagis_pro.Menus
{
    internal class MnuMaps_BtnMapTest : Button
    {
        protected async override void OnClick()
        {
            // Initialize AOI object
            BA_Objects.Aoi oAoi = new BA_Objects.Aoi("animas_AOI_prms", "C:\\Docs\\animas_AOI_prms");
            // Store current AOI in application properties
            Application.Current.Properties[Constants.PROP_AOI] = oAoi;
            Map map = await MapTools.SetDefaultMapFrameNameAsync(Constants.MAPS_DEFAULT_MAP_NAME);
            if (map != null)
            {
                if (map.Layers.Count() > 0)
                {
                    string strMessage = "Adding the maps to the display will overwrite the current arrangement of data layers. " +
                           "This action cannot be undone." + System.Environment.NewLine + "Do you wish to continue ?";
                    MessageBoxResult oRes = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(strMessage, "BAGIS", MessageBoxButton.YesNo);
                    if (oRes != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
        }
    }

}
