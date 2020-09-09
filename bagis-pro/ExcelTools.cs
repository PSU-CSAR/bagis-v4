﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Office.Interop.Excel;
using Microsoft.Office.Core;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Framework.Dialogs;

namespace bagis_pro
{
    class ChartTextBoxSettings
    {
        public int Left;
        public int Top;
        public int Width = Constants.EXCEL_CHART_WIDTH;
        public int Height = Constants.EXCEL_CHART_DESCR_HEIGHT;
        public string Message = "";
    }

    class ExcelTools
    {
        public static async Task<BA_ReturnCode> CreateElevationTableAsync(Worksheet pworksheet, double aoiDemMin)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            await QueuedTask.Run(() =>
            {
                //read class definition for chart and table labeling
                Uri uriElevZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                IList<BA_Objects.Interval> lstInterval = new List<BA_Objects.Interval>();
                bool bElevZonesExist = false;

                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(uriElevZones)))
                { 
                    IReadOnlyList<RasterDatasetDefinition> definitions = geodatabase.GetDefinitions<RasterDatasetDefinition>();
                    foreach (RasterDatasetDefinition def in definitions)
                    {
                        if (def.GetName().Equals(Constants.FILE_ELEV_ZONE))
                        {
                            bElevZonesExist = true;
                            break;
                        }
                    }

                    if (bElevZonesExist)
                    {
                        using (RasterDataset rasterDataset = geodatabase.OpenDataset<RasterDataset>(Constants.FILE_ELEV_ZONE))
                        {
                            RasterBandDefinition bandDefinition = rasterDataset.GetBand(0).GetDefinition();
                            Tuple<double, double> tupleSize = bandDefinition.GetMeanCellSize();
                            if (Math.Round(tupleSize.Item1, 5) != Math.Round(tupleSize.Item2, 5))
                            {
                                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("The X and Y cell size values are not the same for " + uriElevZones.LocalPath + "\\" +
                                        Constants.FILE_ELEV_ZONE + ". This may cause problems with some BAGIS functions!!", "BAGIS-PRO");
                            }
                            double cellSize = (tupleSize.Item1 + tupleSize.Item2) / 2;

                            Raster raster = rasterDataset.CreateDefaultRaster();
                            using (Table rasterTable = raster.GetAttributeTable())
                            {
                                TableDefinition definition = rasterTable.GetDefinition();
                                int idxName = definition.FindField(Constants.FIELD_NAME);
                                int idxLowerBound = definition.FindField(Constants.FIELD_LBOUND);
                                int idxUpperBound = definition.FindField(Constants.FIELD_UBOUND);
                                int idxCount = definition.FindField(Constants.FIELD_COUNT);
                                using (RowCursor cursor = rasterTable.Search())
                                {
                                    while (cursor.MoveNext())
                                    {
                                        BA_Objects.Interval interval = new BA_Objects.Interval();
                                        Row row = cursor.Current;
                                        interval.Value = row[Constants.FIELD_VALUE];
                                        if (idxName > 0)
                                        {
                                            interval.Name = Convert.ToString(row[idxName]);
                                        }
                                        else
                                        {
                                            interval.Name = Constants.VALUE_UNKNOWN;
                                        }
                                        if (idxUpperBound > 0)
                                        {
                                            interval.UpperBound = Convert.ToDouble(row[idxUpperBound]);
                                        }
                                        if (idxLowerBound > 0)
                                        {
                                            interval.LowerBound = Convert.ToDouble(row[idxLowerBound]);
                                        }
                                        if (idxCount > 0)
                                        {
                                            interval.Area = cellSize * Convert.ToInt32(row[idxCount]);
                                        }
                                        lstInterval.Add(interval);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Module1.Current.ModuleLogManager.LogError(nameof(CreateElevationTableAsync),
                            "Unable to locate " + Constants.FILE_ELEV_ZONE + ". The elevation table cannot be created");
                        MessageBox.Show("Unable to locate " + Constants.FILE_ELEV_ZONE +
                                        ". The elevation table cannot be created", "BAGIS-PRO");
                        success = BA_ReturnCode.ReadError;
                        return;
                    }

                //===========================
                //Zonal Statistics
                //===========================
                string strTable = "tblZones";
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, 
                    snapRaster: BA_Objects.Aoi.SnapRasterPath(Module1.Current.Aoi.FilePath), mask: sMask);
                string strInZoneData = uriElevZones.LocalPath + "\\" + Constants.FILE_ELEV_ZONE;
                string strInValueRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, strInValueRaster, strOutTable);
                var gpResult = Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
                if (gpResult.Result.IsFailed)
                {
                    Module1.Current.ModuleLogManager.LogError(nameof(CreateElevationTableAsync),
                        "Failed to reclass " + Constants.FILE_ELEV_ZONE + " raster. Error code: " + gpResult.Result.ErrorCode);
                    foreach (var objMessage in gpResult.Result.Messages)
                    {
                        IGPMessage msg = (IGPMessage)objMessage;
                        Module1.Current.ModuleLogManager.LogError(nameof(CreateElevationTableAsync),
                            msg.Text);
                    }

                    success = BA_ReturnCode.UnknownError;
                }
                else
                {
                    success = BA_ReturnCode.Success;
                }

                //=============================================
                // Create Field Titles
                //=============================================
                pworksheet.Cells[1, 1] = "VALUE";
                pworksheet.Cells[1, 2] = "COUNT";
                pworksheet.Cells[1, 3] = "AREA";
                pworksheet.Cells[1, 4] = "MIN";
                pworksheet.Cells[1, 5] = "MAX";
                pworksheet.Cells[1, 6] = "RANGE";
                pworksheet.Cells[1, 7] = "MEAN";
                pworksheet.Cells[1, 8] = "STD";
                pworksheet.Cells[1, 9] = "SUM";
                pworksheet.Cells[1, 10] = "%_AREA";
                pworksheet.Cells[1, 11] = "%_AREA_ELV";
                pworksheet.Cells[1, 12] = "LABEL";

                //============================================
                // Populate Elevation and Percent Area Rows
                //============================================
                if (success == BA_ReturnCode.Success)
                {
                        using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                        {
                            long rasterValueCount = statisticsTable.GetCount();
                            long sumOfCount = 0;
                            using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                                    }
                                }
                            }

                            double percentArea = 0;
                            int i = 0;
                            using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                            {
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                        percentArea = percentArea + (count / sumOfCount * 100);
                                        // Populate Excel Table
                                        double dblUpperBound = lstInterval[i].UpperBound;
                                        if (!Module1.Current.Settings.m_demDisplayUnits.Equals(Module1.Current.Settings.m_demUnits))
                                        {
                                            if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                                Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                            {
                                                dblUpperBound = LinearUnit.Feet.ConvertTo(dblUpperBound, LinearUnit.Meters);
                                            }
                                            else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                                     Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                            {
                                                dblUpperBound = LinearUnit.Meters.ConvertTo(dblUpperBound, LinearUnit.Feet);
                                            }
                                        }
                                        pworksheet.Cells[i + 3, 1] = dblUpperBound;
                                        pworksheet.Cells[i + 3, 2] = row[Constants.FIELD_COUNT];
                                        pworksheet.Cells[i + 3, 3] = Math.Round(Convert.ToDouble(row["AREA"]), 0);
                                        pworksheet.Cells[i + 3, 4] = row["MIN"];
                                        pworksheet.Cells[i + 3, 5] = row["MAX"];
                                        pworksheet.Cells[i + 3, 6] = row["RANGE"];
                                        pworksheet.Cells[i + 3, 7] = row["MEAN"];
                                        pworksheet.Cells[i + 3, 8] = row["STD"];
                                        pworksheet.Cells[i + 3, 9] = row["SUM"];
                                        pworksheet.Cells[i + 3, 10] = count / sumOfCount * 100;
                                        pworksheet.Cells[i + 3, 11] = percentArea;
                                        pworksheet.Cells[i + 3, 12] = lstInterval[i].Name;
                                        i++;
                                    }
                                }
                            }
                        }
                }
            }   // End Geodatabase using

            //aoiDemMin is always in meters
            //Make First Elevation Interval the Min of AOI DEM
            if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet"))
            {
                aoiDemMin = LinearUnit.Meters.ConvertTo(aoiDemMin, LinearUnit.Feet);
            }
            pworksheet.Cells[2, 1] = aoiDemMin;  //Value
            pworksheet.Cells[2, 11] = 0;         //PERCENT_AREA_ELEVATION
        });
        return success;
    }

        // count the number of records in a worksheet based on the values on the first column
        // aspect, slope, snotel, and snow course tables have a beginning_row value of 1
        // other tables have a value of 2.
        private static long CountRecords(Worksheet pWorksheet, int beginningRow)
        {
            long validRow = 0;
            if (pWorksheet != null)
            {
                long count = pWorksheet.UsedRange.Rows.Count;
                for (int i = beginningRow; i < count; i++)
                {
                    var cellValue = (pWorksheet.UsedRange.Cells[i, 1] as Range).Value;
                    string strCell = Convert.ToString(cellValue);
                    if (! String.IsNullOrEmpty(strCell))
                    {
                        validRow++;
                    }
                }
            }
            return validRow;
        }

        public static async Task<BA_ReturnCode> CreateSitesTableAsync(Worksheet pworksheet, Worksheet pElevWorkSheet, string strZonesFile)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            Uri uriSnotelZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriSnotelZones, strZonesFile);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                //string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, 
                    snapRaster: BA_Objects.Aoi.SnapRasterPath(Module1.Current.Aoi.FilePath),
                    mask: sMask);
                string strInZoneData = uriSnotelZones.LocalPath + "\\" + strZonesFile;
                string strInValueRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_DEM_FILLED;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, strInValueRaster, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "VALUE";
            pworksheet.Cells[1, 2] = "COUNT";
            pworksheet.Cells[1, 3] = "AREA";
            pworksheet.Cells[1, 4] = "MIN";
            pworksheet.Cells[1, 5] = "MAX";
            pworksheet.Cells[1, 6] = "RANGE";
            pworksheet.Cells[1, 7] = "MEAN";
            pworksheet.Cells[1, 8] = "STD";
            pworksheet.Cells[1, 9] = "SUM";
            pworksheet.Cells[1, 10] = "%_AREA";
            pworksheet.Cells[1, 11] = "%_AREA_ELV";
            pworksheet.Cells[1, 12] = "LABEL";

            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                {
                    long rasterValueCount = statisticsTable.GetCount();
                    long sumOfCount = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                            }
                        }
                    }

                    double percentArea = 0;
                    int i = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                percentArea = percentArea + (count / sumOfCount * 100);
                                // Populate Excel Table
                                double dblUpperBound = lstInterval[i].UpperBound;
                                if (!Module1.Current.Settings.m_demDisplayUnits.Equals(Module1.Current.Settings.m_demUnits))
                                {
                                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                        Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                    {
                                        dblUpperBound = LinearUnit.Feet.ConvertTo(dblUpperBound, LinearUnit.Meters);
                                    }
                                    else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                             Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                    {
                                        dblUpperBound = LinearUnit.Meters.ConvertTo(dblUpperBound, LinearUnit.Feet);
                                    }
                                }
                                pworksheet.Cells[i + 2, 1] = dblUpperBound;
                                pworksheet.Cells[i + 2, 2] = row[Constants.FIELD_COUNT];
                                pworksheet.Cells[i + 2, 3] = row["AREA"];
                                pworksheet.Cells[i + 2, 4] = row["MIN"];
                                pworksheet.Cells[i + 2, 5] = row["MAX"];
                                pworksheet.Cells[i + 2, 6] = row["RANGE"];
                                pworksheet.Cells[i + 2, 7] = row["MEAN"];
                                pworksheet.Cells[i + 2, 8] = row["STD"];
                                pworksheet.Cells[i + 2, 9] = row["SUM"];
                                pworksheet.Cells[i + 2, 10] = count / sumOfCount * 100;
                                pworksheet.Cells[i + 2, 11] = percentArea;
                                pworksheet.Cells[i + 2, 12] = lstInterval[i].Name;
                                i++;
                            }
                        }
                    }
                }
            });

            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<double> CreatePrecipitationTableAsync(Worksheet pworksheet, string precipPath,
            double aoiDemMin)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            double MaxPRISMValue = 0.0F;
            Uri uriElevZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriElevZones, Constants.FILE_ELEV_ZONE);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            // We assume elevation zones is the smaller cell size. Could not find api to set cell size to minimum of layers
            double dblCellSize = await GeodatabaseTools.GetCellSizeAsync(uriElevZones, Constants.FILE_ELEV_ZONE);
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: BA_Objects.Aoi.SnapRasterPath(Module1.Current.Aoi.FilePath),
                    mask: sMask, cellSize: dblCellSize);
                string strInZoneData = uriElevZones.LocalPath + "\\" + Constants.FILE_ELEV_ZONE;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, precipPath, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "VALUE";
            pworksheet.Cells[1, 2] = "COUNT";
            pworksheet.Cells[1, 3] = "AREA";
            pworksheet.Cells[1, 4] = "MIN";
            pworksheet.Cells[1, 5] = "MAX";
            pworksheet.Cells[1, 6] = "RANGE";
            pworksheet.Cells[1, 7] = "MEAN";
            pworksheet.Cells[1, 8] = "STD";
            pworksheet.Cells[1, 9] = "SUM";
            pworksheet.Cells[1, 10] = "%_AREA";
            pworksheet.Cells[1, 11] = "Label";
            pworksheet.Cells[1, 12] = "AREA_DEM";
            pworksheet.Cells[1, 13] = "%_AREA_DEM";
            pworksheet.Cells[1, 14] = "VOL_ACRE_FT";
            pworksheet.Cells[1, 15] = "%_VOL";

            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                {
                    long rasterValueCount = statisticsTable.GetCount();
                    long sumOfCount = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                            }
                        }
                    }

                    double percentArea = 0.0F;
                    double TempMaxPRISMValue = 0.0F;
                    int i = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                percentArea = percentArea + (count / sumOfCount * 100);
                                // Populate Excel Table
                                double dblUpperBound = lstInterval[i].UpperBound;
                                if (!Module1.Current.Settings.m_demDisplayUnits.Equals(Module1.Current.Settings.m_demUnits))
                                {
                                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                        Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                    {
                                        dblUpperBound = LinearUnit.Feet.ConvertTo(dblUpperBound, LinearUnit.Meters);
                                    }
                                    else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                             Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                    {
                                        dblUpperBound = LinearUnit.Meters.ConvertTo(dblUpperBound, LinearUnit.Feet);
                                    }
                                }
                                pworksheet.Cells[i + 3, 1] = dblUpperBound;     //@ToDo: Not sure about this?
                                pworksheet.Cells[i + 3, 2] = row[Constants.FIELD_COUNT];
                                pworksheet.Cells[i + 3, 3] = Math.Round(Convert.ToDouble(row["AREA"]), 0);
                                pworksheet.Cells[i + 3, 4] = row["MIN"];
                                pworksheet.Cells[i + 3, 5] = row["MAX"];
                                pworksheet.Cells[i + 3, 6] = row["RANGE"];
                                pworksheet.Cells[i + 3, 7] = row["MEAN"];
                                pworksheet.Cells[i + 3, 8] = row["STD"];
                                pworksheet.Cells[i + 3, 9] = row["SUM"];
                                pworksheet.Cells[i + 3, 10] = count / sumOfCount * 100;         //PERCENT_AREA
                                pworksheet.Cells[i + 3, 11] = lstInterval[i].Name;              //label

                                // Determine Max PRISM Value (for Charting Purposes)
                                TempMaxPRISMValue = (Convert.ToDouble(row[Constants.FIELD_COUNT]) / sumOfCount) * 100;
                                /// find the largest % value
                                if (TempMaxPRISMValue > MaxPRISMValue)
                                {
                                    MaxPRISMValue = TempMaxPRISMValue;
                                }

                                i++;
                            }
                        }
                    }
                    // Make First PRISM Interval the Min of AOI DEM
                    // aoiDemMin is always in meters
                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet"))
                    {
                        aoiDemMin = LinearUnit.Meters.ConvertTo(aoiDemMin, LinearUnit.Feet);
                    }
                    pworksheet.Cells[2, 1] = aoiDemMin; //Value
                    pworksheet.Cells[2, 10] = 0;         // PERCENT_AREA_ELEVATION

                    // ===============================================
                    // Set MaxPRISMValue to an Even Whole Number
                    // ===============================================
                    MaxPRISMValue = Math.Round(MaxPRISMValue * 1.05 + 0.5); // this number is to set the x axis of the chart
                    if (MaxPRISMValue%2 > 0)
                    {
                        MaxPRISMValue = MaxPRISMValue + 1;
                    }
 

                }
            });

             if (success != BA_ReturnCode.Success)
            {
                MaxPRISMValue = -1;
            }
            return MaxPRISMValue;
        }

        public static BA_ReturnCode CopyCells(Worksheet pSourceWS, int SourceCol, 
                                              Worksheet pTargetWS, int TargetCol)
        {
            long row_index = 3;
            Range pRange = pSourceWS.Cells[row_index, SourceCol];

            while (! string.IsNullOrEmpty(pRange.Text.ToString()))
            {
                Range targetRange = pTargetWS.Cells[row_index, TargetCol];
                targetRange.Value = pRange.Value;
                row_index = row_index + 1;
                pRange = pSourceWS.Cells[row_index, SourceCol];
            }
            return BA_ReturnCode.Success;
        }

        public static BA_ReturnCode EstimatePrecipitationVolume(Worksheet pPRSIMWS, int AreaCol, int PrecipCol, 
                                                                int VolumeCol, int PercentCol)
        {
            double conversionfactor;
            int i;

            long row_index = 3;
            Range pRange = pPRSIMWS.Cells[row_index, 1];

            conversionfactor = 1 / (4046.8564224 * 12); // convert sq meter-inch to acre-foot
                                                        // 1 square meter = 2.471053814671653e-4 acre
                                                        // 1 inch = 1/12 feet

            double total_vol = 0;
            int intCount = 0;
            while (!string.IsNullOrEmpty(pRange.Text.ToString()))
            {
                Range areaRange = pPRSIMWS.Cells[row_index, AreaCol];
                Range precipRange = pPRSIMWS.Cells[row_index, PrecipCol];
                Range volumeRange = pPRSIMWS.Cells[row_index, VolumeCol];
                volumeRange.Value = System.Convert.ToDouble(areaRange.Value) * Convert.ToDouble(precipRange.Value) * conversionfactor;
                total_vol = total_vol + System.Convert.ToDouble(volumeRange.Value);
                row_index = row_index + 1;
                pRange = pPRSIMWS.Cells[row_index, 1];
                intCount++;
            }

            // calculate % volume
            for (i = 1; i <= intCount; i++)
            {
                Range pctRange = pPRSIMWS.Cells[i + 2, PercentCol];
                Range volumeRange = pPRSIMWS.Cells[i + 2, VolumeCol];
                pctRange.Value = volumeRange.Value * 100 / total_vol;
            }
            return BA_ReturnCode.Success;
        }

        public static BA_ReturnCode CreateCombinedChart(Worksheet pPRISMWorkSheet, Worksheet pElvWorksheet, Worksheet pChartsWorksheet,
                                                        Worksheet pSNOTELWorksheet, Worksheet pSnowCourseWorkSheet, int topPosition, double Y_Max, double Y_Min,
                                                        double Y_Unit, double MaxPRISMValue)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            long nrecords = ExcelTools.CountRecords(pElvWorksheet, 2);
            long ElevReturn = nrecords + 2;
            nrecords = ExcelTools.CountRecords(pPRISMWorkSheet, 2);
            long PRISMReturn = nrecords + 2;
            long SNOTELReturn = ExcelTools.CountRecords(pSNOTELWorksheet, 1);
            long SnowCourseReturn = ExcelTools.CountRecords(pSnowCourseWorkSheet, 1);
            //@ToDo: Enable when we have start supporting psites
            //nrecords = ExcelTools.CountRecords(pPseudoWorkSheet, 1);
            //long PseudoReturn = nrecords - 1 //not counting the last record, i.e., not presented

            Microsoft.Office.Interop.Excel.Shape myShape = pChartsWorksheet.Shapes.AddChart2();
            Chart myChart = myShape.Chart;

            // Determine Z Mapping Unit and Create Value Axis Title
            string AxisTitleUnit = " (" + Module1.Current.Settings.m_demDisplayUnits + ")";

            // Set SNOTEL Ranges
            string vSNOTELValueRange = "";
            string xSNOTELValueRange = "";
            if (Module1.Current.Aoi.HasSnotel)
            {
                xSNOTELValueRange = "A2:A" + SNOTELReturn;
                vSNOTELValueRange = "K2:K" + SNOTELReturn;
            }

            //Set SnowCourse Ranges
            string vSnowCourseValueRange = "";
            string xSnowCourseValueRange = "";
            if (Module1.Current.Aoi.HasSnowCourse)
            {
                xSnowCourseValueRange = "A2:A" + SnowCourseReturn;
                vSnowCourseValueRange = "K2:K" + SnowCourseReturn;
            }

            //@ToDo: Enable when we enable psites
            //Set Pseudo-site Ranges
            //string vPseudoValueRange = "";
            //string xPseudoValueRange = "";
            //if (Module1.Current.Aoi.HasPseudo)
            //{
            //    xPseudoValueRange = "A2:A" + PseudoReturn;
            //    vPseudoValueRange = "K2:K" + PseudoReturn;
            //}

            // Set PRISM Data Ranges
            string PRISMRange = "";
            string xPRISMValueRange = "";
            string vPRISMValueRange = "";
            PRISMRange = "J3:J" + PRISMReturn;
            xPRISMValueRange = "A3:A" + PRISMReturn;
            vPRISMValueRange = "O3:O" + PRISMReturn;

            // Set Elevation Ranges
            string sElevRange = "";
            string sElevValueRange = "";
            sElevRange = "K2:K" + ElevReturn;
            sElevValueRange = "A2:A" + ElevReturn;

            // Clear Styles
            myChart.ClearToMatchStyle();
            //Insert Title
            myChart.HasTitle = true;
            myChart.HasLegend = true;
            myChart.ChartTitle.Caption = "Area-Elevation, Precipitation  and Site Distribution";
            myChart.ChartTitle.Font.Bold = true;
            //Set Chart Type and Data Range
            myChart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlXYScatter;
            myChart.SetSourceData(pPRISMWorkSheet.Range[PRISMRange]);
            //Set Element Positions
            myChart.SetElement(MsoChartElementType.msoElementChartTitleAboveChart);
            myChart.SetElement(MsoChartElementType.msoElementLegendBottom);
            //Set Chart Position
            myChart.Parent.Left = Constants.EXCEL_CHART_SPACING;
            myChart.Parent.Width = Constants.EXCEL_CHART_WIDTH;
            myChart.Parent.Height = Constants.EXCEL_CHART_HEIGHT;
            myChart.Parent.Top = topPosition;
            // Clear Previous Series
            while (myChart.SeriesCollection().Count > 0)
            {
                myChart.SeriesCollection().Item(1).Delete();
            }

            // Snow Course Series
            Series scSeries;
            System.Drawing.Color color;
            if (Module1.Current.Aoi.HasSnowCourse && SnowCourseReturn > 0)
            {
                scSeries = myChart.SeriesCollection().NewSeries;
                scSeries.Name = "Snow Course";
                //Set Series Values
                scSeries.Values = pSnowCourseWorkSheet.Range[xSnowCourseValueRange];
                scSeries.XValues = pSnowCourseWorkSheet.Range[vSnowCourseValueRange];
                //Set Series Formats
                scSeries.MarkerStyle = Microsoft.Office.Interop.Excel.XlMarkerStyle.xlMarkerStyleTriangle;
                color = System.Drawing.Color.FromArgb(246, 32, 10);
                scSeries.MarkerForegroundColor = System.Drawing.ColorTranslator.ToOle(color);
                scSeries.MarkerBackgroundColor = System.Drawing.ColorTranslator.ToOle(color);
                //Set Axis Group
                scSeries.AxisGroup = Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary;
            }

            // SNOTEL Series
            Series SNOTELSeries;
            if (Module1.Current.Aoi.HasSnotel && SNOTELReturn > 0)
            {
                SNOTELSeries = myChart.SeriesCollection().NewSeries;
                SNOTELSeries.Name = "SNOTEL";
                //Set Series Values
                SNOTELSeries.Values = pSNOTELWorksheet.Range[xSNOTELValueRange];
                SNOTELSeries.XValues = pSNOTELWorksheet.Range[vSNOTELValueRange];
                //Set Series Formats
                SNOTELSeries.MarkerStyle = Microsoft.Office.Interop.Excel.XlMarkerStyle.xlMarkerStyleTriangle;
                SNOTELSeries.MarkerForegroundColor = (int) XlRgbColor.rgbBlack;
                SNOTELSeries.MarkerBackgroundColor = (int) XlRgbColor.rgbBlack;
                //Set Axis Group
                SNOTELSeries.AxisGroup = Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary;
            }

            //@ToDo format pseudo site series when we start supporting psites

            // Elevation Series
            Series ElvSeries = myChart.SeriesCollection().NewSeries;
            ElvSeries.Name = "Elevation";
            // Set Series Values
            ElvSeries.Values = pElvWorksheet.Range[sElevValueRange];
            ElvSeries.XValues = pElvWorksheet.Range[sElevRange];
            // Set Series Formats
            ElvSeries.Smooth = false;
            ElvSeries.Format.Line.DashStyle = MsoLineDashStyle.msoLineSolid;
            color = System.Drawing.Color.FromArgb(74, 126, 187);
            ElvSeries.Format.Line.ForeColor.RGB = System.Drawing.ColorTranslator.ToOle(color);
            ElvSeries.MarkerStyle = Microsoft.Office.Interop.Excel.XlMarkerStyle.xlMarkerStyleNone;
            // Set Axis Group
            ElvSeries.AxisGroup = Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary;
            //Set to be first plotted series
            ElvSeries.PlotOrder = 1;

            //PRISM Series
            Series PRISM = myChart.SeriesCollection().NewSeries;
            PRISM.Name = "Precipitation";
            // Set Series Values
            PRISM.Values = pPRISMWorkSheet.Range[xPRISMValueRange];
            PRISM.XValues = pPRISMWorkSheet.Range[vPRISMValueRange];
            // Set Series Formats
            PRISM.Smooth = false;
            PRISM.Format.Line.DashStyle = MsoLineDashStyle.msoLineSolid;
            color = System.Drawing.Color.FromArgb(204, 0, 0);
            PRISM.Format.Line.ForeColor.RGB = System.Drawing.ColorTranslator.ToOle(color);
            PRISM.Format.Line.BackColor.RGB = System.Drawing.ColorTranslator.ToOle(color);
            PRISM.MarkerStyle = Microsoft.Office.Interop.Excel.XlMarkerStyle.xlMarkerStyleSquare;
            //Set Axis Group
            PRISM.AxisGroup = Microsoft.Office.Interop.Excel.XlAxisGroup.xlSecondary;
            //Set to be first plotted series
            PRISM.PlotOrder = 1;

            // Set Variables Associates with each Axis
            // Bottom Axis
            Axis axis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlCategory, 
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            axis.HasTitle = true;
            axis.AxisTitle.Characters.Text = "% AOI Area below Elevation";
            axis.AxisTitle.Orientation = 0;
            axis.AxisTitle.Font.Bold = true;
            axis.MaximumScale = 100.1;
            axis.MinimumScale = 0.0F;

            // Left Side Axis
            axis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlValue,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            axis.HasTitle = true;
            axis.AxisTitle.Characters.Text = "Elevation" + AxisTitleUnit;
            axis.AxisTitle.Orientation = 90;
            axis.AxisTitle.Font.Bold = true;
            axis.MaximumScale = Y_Max;
            axis.MinimumScale = Y_Min;
            axis.MajorUnit = Y_Unit;

            // Right Side Axis
            axis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlValue,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlSecondary);
            axis.HasTitle = true;
            axis.AxisTitle.Characters.Text = "Elevation" + AxisTitleUnit;
            axis.AxisTitle.Font.Bold = true;
            axis.AxisTitle.Orientation = 90;
            axis.MaximumScale = Y_Max;
            axis.MinimumScale = Y_Min;
            axis.MajorUnit = Y_Unit;

            // Top Axis
            axis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlCategory,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlSecondary);
            axis.HasTitle = true;
            axis.AxisTitle.Characters.Text = "Precipitation Distribution (% contribution by elevation zone)";
            axis.AxisTitle.Font.Bold = true;
            axis.AxisTitle.Orientation = "0";
            axis.MaximumScale = MaxPRISMValue;
            axis.MinimumScale = 0.0F; ;

            // Insert Axes
            myChart.HasAxis[Microsoft.Office.Interop.Excel.XlAxisType.xlCategory, Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary] = true;
            myChart.HasAxis[Microsoft.Office.Interop.Excel.XlAxisType.xlCategory, Microsoft.Office.Interop.Excel.XlAxisGroup.xlSecondary] = true;
            myChart.HasAxis[Microsoft.Office.Interop.Excel.XlAxisType.xlValue, Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary] = true;
            myChart.HasAxis[Microsoft.Office.Interop.Excel.XlAxisType.xlValue, Microsoft.Office.Interop.Excel.XlAxisGroup.xlSecondary] = true;

            // Descriptive textbox
            StringBuilder sb = new StringBuilder();
            sb.Append("Area-Elevation, Precipitation and Site Distribution chart \r\n");
            sb.Append("The chart shows the percentage of the precipitation contributed by the user-specified elevation intervals");
            sb.Append("and the snow monitoring sites plotted on the Area-Elevation Distribution curve according to the sites'");
            sb.Append("elevation. The chart tells if the snow monitoring sites record the major precipitation in the AOI.");
            ChartTextBoxSettings textBoxSettings = new ChartTextBoxSettings
            {
                Left = Constants.EXCEL_CHART_SPACING,
                Top = topPosition + Constants.EXCEL_CHART_HEIGHT + 10,
                Message = sb.ToString()
            };
            pChartsWorksheet.Shapes.AddTextbox(MsoTextOrientation.msoTextOrientationHorizontal, textBoxSettings.Left,
                                               textBoxSettings.Top, textBoxSettings.Width, textBoxSettings.Height).
                                               TextFrame.Characters().Text = textBoxSettings.Message;
            success = BA_ReturnCode.Success;
            return success;
        }

        public static double ConfigureYAxis(double minvalue, double maxvalue, double interval, 
            ref double Chart_YMaxScale)
        {
            // returning the min scale
            double chart_YMinScale = -99.0f;
            int quotient = (int) (minvalue / interval);
            chart_YMinScale = quotient * interval;
            int modvalue = (int) (maxvalue % interval);
            if (modvalue == 0)
            {
                quotient = (int) (maxvalue / interval);
            }
            else
            {
                quotient = (int) (maxvalue / interval) + 1;
            }

            Chart_YMaxScale = quotient * interval;  // Setting the max scale by ref
            return chart_YMinScale;
        }

        public static async Task<BA_ReturnCode> CreateSlopeTableAsync(Worksheet pworksheet)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            //read class definition for chart and table labeling
            Uri uriSlopeZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriSlopeZones, Constants.FILE_SLOPE_ZONE);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: BA_Objects.Aoi.SnapRasterPath(Module1.Current.Aoi.FilePath),
                    mask: sMask);
                string strInZoneData = uriSlopeZones.LocalPath + "\\" + Constants.FILE_SLOPE_ZONE;
                string strInValueRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_SLOPE;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, strInValueRaster, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "SLOPE";
            pworksheet.Cells[1, 2] = "COUNT";
            pworksheet.Cells[1, 3] = "AREA";
            pworksheet.Cells[1, 4] = "MIN";
            pworksheet.Cells[1, 5] = "MAX";
            pworksheet.Cells[1, 6] = "RANGE";
            pworksheet.Cells[1, 7] = "MEAN";
            pworksheet.Cells[1, 8] = "STD";
            pworksheet.Cells[1, 9] = "SUM";
            pworksheet.Cells[1, 10] = "%_AREA";
            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                {
                    long rasterValueCount = statisticsTable.GetCount();
                    long sumOfCount = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                            }
                        }
                    }

                    int i = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                pworksheet.Cells[i + 2, 1] = lstInterval[i].Name;
                                pworksheet.Cells[i + 2, 2] = row[Constants.FIELD_COUNT];
                                pworksheet.Cells[i + 2, 3] = Math.Round(Convert.ToDouble(row["AREA"]), 0);
                                pworksheet.Cells[i + 2, 4] = row["MIN"];
                                pworksheet.Cells[i + 2, 5] = row["MAX"];
                                pworksheet.Cells[i + 2, 6] = row["RANGE"];
                                pworksheet.Cells[i + 2, 7] = row["MEAN"];
                                pworksheet.Cells[i + 2, 8] = row["STD"];
                                pworksheet.Cells[i + 2, 9] = row["SUM"];
                                pworksheet.Cells[i + 2, 10] = count / sumOfCount * 100;
                                i++;
                            }
                        }
                    }
                }
            });

            success = BA_ReturnCode.Success;
            return success;
        }

        public static BA_ReturnCode CreateSlopeChart(Worksheet pSlopeWorksheet, Worksheet pChartsWorksheet,
            int topPosition, int leftPosition)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;
            long nrecords = ExcelTools.CountRecords(pSlopeWorksheet, 1);

            // ===========================
            // Make Chart
            // ===========================
            Microsoft.Office.Interop.Excel.Shape myShape = pChartsWorksheet.Shapes.AddChart2();
            Chart myChart = myShape.Chart;

            string ValueRange = "A2:A" + (nrecords + 1) + "," + "J2:J" + (nrecords + 1);

            // Clear Styles
            myChart.ClearToMatchStyle();
            // Set Title
            myChart.HasTitle = true;
            myChart.ChartTitle.Caption = "Slope Distribution";
            myChart.ChartTitle.Font.Bold = true;
            // Set Position and Location
            myChart.Parent.Left = leftPosition;
            myChart.Parent.Width = Constants.EXCEL_CHART_WIDTH;
            myChart.Parent.Top = topPosition;
            myChart.Parent.Height = Constants.EXCEL_CHART_HEIGHT;
            // Set Chart Type and Value Range
            myChart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlColumnClustered;
            myChart.SetSourceData(pSlopeWorksheet.Range[ValueRange]);
            
            // Set Axis Properties
            Axis categoryAxis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlCategory,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            Axis valueAxis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlValue,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            categoryAxis.HasTitle = true;
            categoryAxis.AxisTitle.Characters.Text = "Percent Slope";
            categoryAxis.AxisTitle.Font.Bold = true;
            valueAxis.HasTitle = true;
            valueAxis.AxisTitle.Font.Bold = true;
            valueAxis.AxisTitle.Characters.Text = "% AOI Area";
            valueAxis.MinimumScale = 0;
            // Set Element Positions
            myChart.SetElement(MsoChartElementType.msoElementChartTitleAboveChart);
            myChart.SetElement(MsoChartElementType.msoElementLegendNone);

            // Descriptive textbox
            StringBuilder sb = new StringBuilder();
            sb.Append("Slope Distribution chart \r\n");
            sb.Append("The chart shows the percentage of AOI area in each slope interval.");
            ChartTextBoxSettings textBoxSettings = new ChartTextBoxSettings
            {
                Left = leftPosition,
                Top = topPosition + Constants.EXCEL_CHART_HEIGHT + 10,
                Message = sb.ToString()
            };
            pChartsWorksheet.Shapes.AddTextbox(MsoTextOrientation.msoTextOrientationHorizontal, textBoxSettings.Left,
                                               textBoxSettings.Top, textBoxSettings.Width, textBoxSettings.Height).
                                               TextFrame.Characters().Text = textBoxSettings.Message;

            success = BA_ReturnCode.Success;
            return success;
        }

        public static async Task<BA_ReturnCode> CreateAspectTableAsync(Worksheet pworksheet)
        {
            BA_ReturnCode success = BA_ReturnCode.UnknownError;

            //read class definition for chart and table labeling
            Uri uriAspectZones = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            IList<BA_Objects.Interval> lstInterval = await GeodatabaseTools.ReadReclassRasterAttribute(uriAspectZones, Constants.FILE_ASPECT_ZONE);

            //===========================
            //Zonal Statistics
            //===========================
            string strTable = "tblZones";
            IGPResult gpResult = await QueuedTask.Run(() =>
            {
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_RASTER;
                var environments = Geoprocessing.MakeEnvironmentArray(workspace: Module1.Current.Aoi.FilePath, snapRaster: BA_Objects.Aoi.SnapRasterPath(Module1.Current.Aoi.FilePath),
                    mask: sMask);
                string strInZoneData = uriAspectZones.LocalPath + "\\" + Constants.FILE_ASPECT_ZONE;
                string strInValueRaster = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Surfaces, true) + Constants.FILE_ASPECT;
                string strOutTable = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, true) + strTable;
                var parameters = Geoprocessing.MakeValueArray(strInZoneData, Constants.FIELD_VALUE, strInValueRaster, strOutTable);
                return Geoprocessing.ExecuteToolAsync("ZonalStatisticsAsTable", parameters, environments,
                            CancelableProgressor.None, GPExecuteToolFlags.AddToHistory);
            });
            if (gpResult.IsFailed)
            {
                success = BA_ReturnCode.UnknownError;
            }
            else
            {
                success = BA_ReturnCode.Success;
            }

            //=============================================
            // Create Field Titles
            //=============================================
            pworksheet.Cells[1, 1] = "DIRECTION";
            pworksheet.Cells[1, 2] = "DIRECTION";
            pworksheet.Cells[1, 3] = "COUNT";
            pworksheet.Cells[1, 4] = "AREA";
            pworksheet.Cells[1, 5] = "MIN";
            pworksheet.Cells[1, 6] = "MAX";
            pworksheet.Cells[1, 7] = "RANGE";
            pworksheet.Cells[1, 8] = "MEAN";
            pworksheet.Cells[1, 9] = "STD";
            pworksheet.Cells[1, 10] = "SUM";
            pworksheet.Cells[1, 11] = "%_AREA";
            //============================================
            // Populate Elevation and Percent Area Rows
            //============================================
            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(strTable))
                {
                    long rasterValueCount = statisticsTable.GetCount();
                    long sumOfCount = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                sumOfCount = sumOfCount + Convert.ToInt32(row["COUNT"]);
                            }
                        }
                    }

                    int i = 0;
                    using (RowCursor rowCursor = statisticsTable.Search(new QueryFilter(), false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (Row row = rowCursor.Current)
                            {
                                double count = Convert.ToDouble(row[Constants.FIELD_COUNT]);
                                // Populate Excel Table
                                pworksheet.Cells[i + 2, 1] = row["VALUE"];
                                pworksheet.Cells[i + 2, 2] = lstInterval[i].Name;
                                pworksheet.Cells[i + 2, 3] = row[Constants.FIELD_COUNT];
                                pworksheet.Cells[i + 2, 4] = Math.Round(Convert.ToDouble(row["AREA"]), 0);
                                pworksheet.Cells[i + 2, 5] = row["MIN"];
                                pworksheet.Cells[i + 2, 6] = row["MAX"];
                                pworksheet.Cells[i + 2, 7] = row["RANGE"];
                                pworksheet.Cells[i + 2, 8] = row["MEAN"];
                                pworksheet.Cells[i + 2, 9] = row["STD"];
                                pworksheet.Cells[i + 2, 10] = row["SUM"];
                                pworksheet.Cells[i + 2, 11] = count / sumOfCount * 100;
                                i++;
                            }
                        }
                    }
                }
            });

            success = BA_ReturnCode.Success;
            return success;
        }

        public static BA_ReturnCode CreateAspectChart(Worksheet pAspectWorksheet, Worksheet pChartsWorksheet,
            int topPosition, int leftPosition)
        {
            long nrecords = ExcelTools.CountRecords(pAspectWorksheet, 1);

            // ===========================
            // Make Chart
            // ===========================
            Microsoft.Office.Interop.Excel.Shape myShape = pChartsWorksheet.Shapes.AddChart2();
            Chart myChart = myShape.Chart;

            string ValueRange = "B2:B" + (nrecords + 1) + "," + "K2:K" + (nrecords + 1);

            // Clear Styles
            myChart.ClearToMatchStyle();
            // Set Title
            myChart.HasTitle = true;
            myChart.ChartTitle.Caption = "Aspect Distribution";
            myChart.ChartTitle.Font.Bold = true;
            // Set Position and Location
            myChart.Parent.Left = leftPosition;
            myChart.Parent.Width = Constants.EXCEL_CHART_WIDTH;
            myChart.Parent.Top = topPosition;
            myChart.Parent.Height = Constants.EXCEL_CHART_HEIGHT;
            // Set Chart Type and Value Range
            myChart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlColumnClustered;
            myChart.SetSourceData(pAspectWorksheet.Range[ValueRange]);
            // Set Axis Parameters
            Axis categoryAxis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlCategory,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            Axis valueAxis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlValue,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            categoryAxis.HasTitle = true;
            categoryAxis.AxisTitle.Characters.Text = "Aspect";
            categoryAxis.AxisTitle.Font.Bold = true;
            valueAxis.HasTitle = true;
            valueAxis.AxisTitle.Font.Bold = true;
            valueAxis.AxisTitle.Characters.Text = "% AOI Area";
            valueAxis.MinimumScale = 0;
            // Set Element Positions
            myChart.SetElement(MsoChartElementType.msoElementChartTitleAboveChart);
            myChart.SetElement(MsoChartElementType.msoElementLegendNone);

            // Descriptive textbox
            StringBuilder sb = new StringBuilder();
            sb.Append("Aspect Distribution chart \r\n");
            sb.Append("The chart shows the percentage of AOI area in each aspect direction.");
            ChartTextBoxSettings textBoxSettings = new ChartTextBoxSettings
            {
                Left = leftPosition,
                Top = topPosition + Constants.EXCEL_CHART_HEIGHT + 10,
                Message = sb.ToString()
            };
            pChartsWorksheet.Shapes.AddTextbox(MsoTextOrientation.msoTextOrientationHorizontal, textBoxSettings.Left,
                                               textBoxSettings.Top, textBoxSettings.Width, textBoxSettings.Height).
                                               TextFrame.Characters().Text = textBoxSettings.Message;
            return BA_ReturnCode.Success;
        }

        public static async Task<int> CreateRepresentPrecipTableAsync(Worksheet pworksheet, string precipPath)
        {
            //=============================================
            //Create Field Titles
            //=============================================
            int idxPrecipExcelCol = 1;
            int idxElevExcelCol = 2;
            int idxAspectExcelCol = 3;

            pworksheet.Cells[1, idxPrecipExcelCol] = "Precipitation (" + Constants.UNITS_INCHES + ")";
            pworksheet.Cells[1, idxElevExcelCol] = "Elevation (" + Module1.Current.Settings.m_demDisplayUnits + ")";
            pworksheet.Cells[1, idxAspectExcelCol] = "ASPECT";

            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            double minPrecipValue = 999.0F;
            await QueuedTask.Run(() => {
                using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
                using (Table statisticsTable = geodatabase.OpenDataset<Table>(Constants.FILE_ASP_ZONE_PREC_TBL))
                {
                    TableDefinition definition = statisticsTable.GetDefinition();
                    string prismFieldName = System.IO.Path.GetFileName(precipPath) + "_band_1";
                    string elevFieldName = Constants.FILE_PREC_MEAN_ELEV + "_band_1";
                    int idxPrecipTableCol = definition.FindField(prismFieldName);
                    int idxElevTableCol = definition.FindField(elevFieldName);
                    int idxAspectTableCol = definition.FindField(Constants.FIELD_ASPECT);

                    if (idxPrecipTableCol > -1 && idxElevTableCol > -1 && idxAspectTableCol > -1)
                    {
                        QueryFilter pQFilter = new QueryFilter();
                        pQFilter.WhereClause = prismFieldName + " is not null and " + elevFieldName + " is not null";
                        int idxRow = 2;
                        using (RowCursor cursor = statisticsTable.Search(pQFilter, false))
                        {
                            while (cursor.MoveNext())
                            {
                                Row pRow = cursor.Current;
                                double precip = Convert.ToDouble(pRow[idxPrecipTableCol]);
                                pworksheet.Cells[idxRow, idxPrecipExcelCol] = precip;
                                if (precip < minPrecipValue)
                                    minPrecipValue = precip;
                                double elevation = Convert.ToDouble(pRow[idxElevTableCol]);
                                if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                    Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                {
                                    elevation = LinearUnit.Feet.ConvertTo(elevation, LinearUnit.Meters);
                                }
                                else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                         Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                {
                                    elevation = LinearUnit.Meters.ConvertTo(elevation, LinearUnit.Feet);
                                }
                                pworksheet.Cells[idxRow, idxElevExcelCol] = elevation;
                                string aspect = Convert.ToString(pRow[idxAspectTableCol]);
                                if (string.IsNullOrEmpty(aspect))
                                {
                                    aspect = "Unknown";
                                }
                                pworksheet.Cells[idxRow, idxAspectExcelCol] = aspect;
                                idxRow++;
                            }
                        }
                    }
                }
            });
            return (int)minPrecipValue;
        }

        public static async Task<BA_ReturnCode> CreateSnotelPrecipTableAsync(Worksheet pworksheet,
            IList<BA_Objects.Site> lstSelectedSites)
        {
            //=============================================
            //Create Field Titles
            //=============================================
            int idxElevExcelCol = 1;
            int idxNameExcelCol = 2;
            int idxTypeExcelCol = 3;
            int idxPrecipExcelCol = 4;
            int idxAspectExcelCol = 5;

            pworksheet.Cells[1, idxElevExcelCol] = Constants.FIELD_SITE_ELEV;
            pworksheet.Cells[1, idxNameExcelCol] = Constants.FIELD_SITE_NAME;
            pworksheet.Cells[1, idxTypeExcelCol] = Constants.FIELD_SITE_TYPE;
            //RASTERVALU after extract values to points
            pworksheet.Cells[1, idxPrecipExcelCol] = "Precipitation (" + Constants.UNITS_INCHES + ")";
            pworksheet.Cells[1, idxAspectExcelCol] = Constants.FIELD_ASPECT;

            Uri analysisUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
            await QueuedTask.Run(() => {
            using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(analysisUri)))
            using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(Constants.FILE_PREC_STEL))
            {
                FeatureClassDefinition definition = featureClass.GetDefinition();
                int idxPrecipTableCol = definition.FindField(Constants.FIELD_PRECIP);
                int idxElevTableCol = definition.FindField(Constants.FIELD_SITE_ELEV);
                int idxNameTableCol = definition.FindField(Constants.FIELD_SITE_NAME);
                int idxTypeTableCol = definition.FindField(Constants.FIELD_SITE_TYPE);
                int idxAspectTableCol = definition.FindField(Constants.FIELD_ASPECT);

                if (idxPrecipTableCol > -1 && idxElevTableCol > -1 && idxNameTableCol > -1 &&
                    idxTypeTableCol > -1 && idxAspectTableCol > -1)
                {
                    QueryFilter pQFilter = new QueryFilter();
                    using (RowCursor cursor = featureClass.Search(pQFilter, false))
                    {
                        int idxRow = 2;
                        while (cursor.MoveNext())
                        {
                            Row pRow = cursor.Current;
                            bool bAddRow = true;
                            if (lstSelectedSites.Count > 0)
                            {
                                bAddRow = false;
                                string strName = Convert.ToString(pRow[idxNameTableCol]);
                                double dblElevation = Convert.ToDouble(pRow[idxNameExcelCol]);
                                string strSiteType = Convert.ToString(pRow[idxTypeTableCol]);
                                    foreach (var aSite in lstSelectedSites)
                                    {
                                        if (strName.Equals(aSite.Name) && dblElevation == aSite.Elevation)
                                        {
                                            if (aSite.SiteType.Equals(SiteType.Snotel) && strSiteType.Equals(Constants.SITE_TYPE_SNOTEL))
                                            {
                                                bAddRow = true;
                                                break;
                                            }
                                            else if (aSite.SiteType.Equals(SiteType.SnowCourse) && strSiteType.Equals(Constants.SITE_TYPE_SNOW_COURSE))
                                            {
                                                bAddRow = true;
                                                break;
                                            }
                                            else if (aSite.SiteType.Equals(SiteType.Pseudo) && strSiteType.Equals(Constants.SITE_TYPE_PSEUDO))
                                            {
                                                bAddRow = true;
                                                break;
                                            }
                                        }
                                    }
                            }
                            if (bAddRow == true)
                                {
                                    pworksheet.Cells[idxRow, idxPrecipExcelCol] = Convert.ToDouble(pRow[idxPrecipTableCol]);
                                    string aspect = Convert.ToString(pRow[idxAspectTableCol]);
                                    if (string.IsNullOrEmpty(aspect))
                                    {
                                        aspect = "Unknown";
                                    }
                                    pworksheet.Cells[idxRow, idxAspectExcelCol] = aspect;
                                    double elevation = Convert.ToDouble(pRow[idxElevTableCol]);
                                    if (Module1.Current.Settings.m_demDisplayUnits.Equals("Meters") &&
                                        Module1.Current.Settings.m_demUnits.Equals("Feet"))
                                    {
                                        elevation = LinearUnit.Feet.ConvertTo(elevation, LinearUnit.Meters);
                                    }
                                    else if (Module1.Current.Settings.m_demDisplayUnits.Equals("Feet") &&
                                             Module1.Current.Settings.m_demUnits.Equals("Meters"))
                                    {
                                        elevation = LinearUnit.Meters.ConvertTo(elevation, LinearUnit.Feet);
                                    }
                                    pworksheet.Cells[idxRow, idxElevExcelCol] = elevation; pworksheet.Cells[idxRow, idxNameExcelCol] = Convert.ToString(pRow[idxNameTableCol]);
                                    pworksheet.Cells[idxRow, idxTypeExcelCol] = Convert.ToString(pRow[idxTypeTableCol]);

                                    idxRow++;
                                }

                            }
                    }
                }
            }

            });
            return BA_ReturnCode.Success;
        }

        public static BA_ReturnCode CreateRepresentPrecipChart(Worksheet pPrecipElvWorksheet, Worksheet pPrecipSiteWorksheet,
            Worksheet pChartsWorksheet, int intMinPrecip, double minValue)
        {
            // ===========================
            // Make Chart
            // ===========================
            Microsoft.Office.Interop.Excel.Shape myShape = pChartsWorksheet.Shapes.AddChart2();
            Chart myChart = myShape.Chart;

            int legendTop = 25;
            int plotWidth = 575;

            // Clear Styles
            myChart.ClearToMatchStyle();
            // Set Title
            myChart.HasTitle = true;
            myChart.HasLegend = true;
            myChart.ChartTitle.Caption = "Elevation Precipitation";
            myChart.ChartTitle.Font.Bold = true;
            // Set Chart Type and Data Range
            myChart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlXYScatter;
            // Set Position and Location
            myChart.Parent.Left = Constants.EXCEL_CHART_SPACING;
            myChart.Parent.Width = Constants.EXCEL_LARGE_CHART_WIDTH;
            myChart.Parent.Top = Constants.EXCEL_CHART_SPACING;
            myChart.Parent.Height = Constants.EXCEL_LARGE_CHART_HEIGHT;

            // Set series for precip/elevation values
            long nrecords = ExcelTools.CountRecords(pPrecipElvWorksheet, 1);
            string precipValueRange = "A2:A" + (nrecords + 1) ;
            string xDemValueRange = "B2:B" + (nrecords + 1);

            // precip/elevation values scatterplot for each cell
            Series series = myChart.SeriesCollection().NewSeries;
            series.Name = "AOI";
            //Set Series Values
            series.Values = pPrecipElvWorksheet.Range[precipValueRange];
            series.XValues = pPrecipElvWorksheet.Range[xDemValueRange];
            //Set Series Formats
            series.MarkerStyle = Microsoft.Office.Interop.Excel.XlMarkerStyle.xlMarkerStyleDiamond;
            series.MarkerSize = 7;

            // trendline for aoi dataset
            Microsoft.Office.Interop.Excel.Trendlines trendlines = series.Trendlines();
            Trendline trendline = trendlines.Add(Microsoft.Office.Interop.Excel.XlTrendlineType.xlLinear, Type.Missing,
                Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                true, true, "Linear (AOI)");
            trendline.DataLabel.Left = 710;
            trendline.DataLabel.Top = legendTop + 100;
            trendline.DataLabel.Font.Size = 20;
            trendline.DataLabel.Font.Color = (int)XlRgbColor.rgbBlue;
            trendline.Format.Line.Weight = 1.5F;
            trendline.Format.Line.DashStyle = MsoLineDashStyle.msoLineSolid;
            trendline.Format.Line.ForeColor.RGB = (int)XlRgbColor.rgbBlue;

            // Set series for SNOTEL precip/elevation values
            nrecords = ExcelTools.CountRecords(pPrecipSiteWorksheet, 1);
            precipValueRange = "D2:D" + (nrecords + 1);
            string xElevValueRange = "A2:A" + (nrecords + 1);
            Series ser2 = myChart.SeriesCollection().NewSeries;
            ser2.Name = "Sites";
            //Set ser2 Values
            ser2.Values = pPrecipSiteWorksheet.Range[precipValueRange];
            ser2.XValues = pPrecipSiteWorksheet.Range[xElevValueRange];
            //Set ser2 Formats
            ser2.MarkerStyle = Microsoft.Office.Interop.Excel.XlMarkerStyle.xlMarkerStyleSquare;
            ser2.MarkerSize = 7;
            ser2.MarkerBackgroundColor = (int)XlRgbColor.rgbRed;
            ser2.MarkerForegroundColor = (int)XlRgbColor.rgbRed;

            if (nrecords > 1)
            {
                Microsoft.Office.Interop.Excel.Trendlines trendlines2 = ser2.Trendlines();
                Trendline trendline2 = trendlines2.Add(Microsoft.Office.Interop.Excel.XlTrendlineType.xlLinear, Type.Missing,
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                    true, true, "Linear (Sites)");
                trendline2.DataLabel.Left = 710;
                trendline2.DataLabel.Top = legendTop + 180;
                trendline2.DataLabel.Font.Size = 20;
                trendline2.DataLabel.Font.Color = (int)XlRgbColor.rgbOrange;
                trendline2.Format.Line.Weight = 1.5F;
                trendline2.Format.Line.DashStyle = MsoLineDashStyle.msoLineSolid;
                trendline2.Format.Line.ForeColor.RGB = (int)XlRgbColor.rgbOrange;
            }
            else
            {
                MessageBox.Show("Warning: Your sites data did not contain enough sites to generate a trendline!", "BAGIS-PRO");
                Module1.Current.ModuleLogManager.LogError(nameof(CreateRepresentPrecipChart),
                    "Your sites data did not contain enough sites to generate a trendline!");
            }

            // Set Element Positions
            myChart.SetElement(MsoChartElementType.msoElementChartTitleAboveChart);
            myChart.SetElement(MsoChartElementType.msoElementLegendRight);
            myChart.Legend.Top = legendTop;
            myChart.Legend.Left = plotWidth + 20;
            myChart.PlotArea.Width = plotWidth;

            // Left Side Axis
            Axis yAxis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlValue,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            yAxis.HasTitle = true;
            yAxis.AxisTitle.Text = "Precipitation (" + Constants.UNITS_INCHES + ")";
            yAxis.AxisTitle.Orientation = 90;
            yAxis.MinimumScale = intMinPrecip - 1;

            // Bottom Axis
            Axis categoryAxis = (Axis)myChart.Axes(Microsoft.Office.Interop.Excel.XlAxisType.xlCategory,
                Microsoft.Office.Interop.Excel.XlAxisGroup.xlPrimary);
            categoryAxis.HasTitle = true;
            categoryAxis.AxisTitle.Text = "Elevation (" + Module1.Current.Settings.m_demDisplayUnits + ")";
            categoryAxis.AxisTitle.Orientation = 0;
            // minValue was already converted to display units by the calling function
            categoryAxis.MinimumScale = (int) minValue;

            return BA_ReturnCode.Success;

        }

        }

    }
