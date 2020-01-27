﻿using ArcGIS.Desktop.Core.Geoprocessing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    class GeoprocessingTools
    {
        public static async Task<IList<double>> GetDemStatsAsync(string aoiPath, string maskPath, double adjustmentFactor)
        {
            IList<double> returnList = new List<double>();
            try
            {
                string sDemPath = GeodatabaseTools.GetGeodatabasePath(aoiPath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                double dblMin = -1;
                var parameters = Geoprocessing.MakeValueArray(sDemPath, "MINIMUM");
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: aoiPath, mask: maskPath);              
                IGPResult gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    ArcGIS.Desktop.Framework.Threading.Tasks.CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                bool success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMin);
                returnList.Add(dblMin - adjustmentFactor);
                double dblMax = -1;
                parameters = Geoprocessing.MakeValueArray(sDemPath, "MAXIMUM");
                gpResult = await Geoprocessing.ExecuteToolAsync("GetRasterProperties_management", parameters, environments,
                    ArcGIS.Desktop.Framework.Threading.Tasks.CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                success = Double.TryParse(Convert.ToString(gpResult.ReturnValue), out dblMax);
                returnList.Add(dblMax + adjustmentFactor);
            }
            catch (Exception e)
            {
                Debug.WriteLine("GetDemStatsAsync: " + e.Message);
            }
            return returnList;
        }
    }
}
