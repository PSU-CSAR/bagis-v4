﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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


namespace bagis_pro.Buttons
{
    internal class ToggleMapDisplay
    {
        public static async Task Toggle(BagisMapType mapType)
        {
            //Get map definition
            BA_Objects.MapDefinition thisMap = MapTools.LoadMapDefinition(mapType);
            
            // toggle layers according to map definition
            var allLayers = MapView.Active.Map.Layers.ToList();
            await QueuedTask.Run(() =>
            {
                foreach (var layer in allLayers)
                {
                    if (thisMap.LayerList.Contains(layer.Name))
                    {
                        layer.SetVisibility(true);
                    }
                    else
                    {
                        layer.SetVisibility(false);
                    }
                }
            });

            Layout layout = await MapTools.GetDefaultLayoutAsync(Constants.MAPS_DEFAULT_LAYOUT_NAME);
            await MapTools.UpdateMapElementsAsync(layout, Module1.Current.Aoi.Name.ToUpper(), thisMap);
            await MapTools.UpdateLegendAsync(layout, thisMap);
            Module1.Current.DisplayedMap = thisMap.PdfFileName;
        }
    }

    internal class MapButtonPalette_BtnElevation : Button
    {
        protected async override void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.ELEVATION);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display elevation map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSlope : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.SLOPE);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display slope map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnAspect : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.ASPECT);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display aspect map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSnotel : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.SNOTEL);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display snotel map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSnowCourse : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.SCOS);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display snow course map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnSitesAll : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.SITES_ALL);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display all sites map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnJanSwe : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.SNODAS_SWE);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

    internal class MapButtonPalette_BtnPrism : Button
    {
        protected override async void OnClick()
        {
            try
            {
                Module1.Current.MapFinishedLoading = false;
                await ToggleMapDisplay.Toggle(BagisMapType.PRISM);
                Module1.Current.MapFinishedLoading = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to display SNODAS SWE map!!" + e.Message, "BAGIS-PRO");
            }
        }
    }

}
