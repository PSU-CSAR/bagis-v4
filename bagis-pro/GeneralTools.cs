﻿using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace bagis_pro
{
    public class GeneralTools
    {

        public static string GetAddInDirectory()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            return System.IO.Path.GetDirectoryName(
                              Uri.UnescapeDataString(
                                      new Uri(asm.CodeBase).LocalPath));
        }

        public static async Task<BA_ReturnCode> ExportMapToPdfAsync()
        {
            try
            {
                //Export a single page layout to PDF.

                //Create a PDF format with appropriate settings
                //BMP, EMF, EPS, GIF, JPEG, PNG, SVG, TGA, and TFF formats are also available for export
                PDFFormat PDF = new PDFFormat()
                {
                    OutputFileName = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE + "\\"
                    + Module1.Current.DisplayedMap,
                    Resolution = 300,
                    DoCompressVectorGraphics = true,
                    DoEmbedFonts = true,
                    HasGeoRefInfo = true,
                    ImageCompression = ImageCompression.Adaptive,
                    ImageQuality = ImageQuality.Best,
                    LayersAndAttributes = LayersAndAttributes.LayersAndAttributes
                };

                // Get a handle to the layout
                Layout layout = null;
                await QueuedTask.Run(() =>
                {
                    LayoutProjectItem lytItem =
                    ArcGIS.Desktop.Core.Project.Current.GetItems<LayoutProjectItem>()
                        .FirstOrDefault(m => m.Name.Equals(Constants.MAPS_DEFAULT_LAYOUT_NAME, StringComparison.CurrentCultureIgnoreCase));
                    if (lytItem != null)
                    {
                        layout = lytItem.GetLayout();
                    }
                });

                //Check to see if the path is valid and export
                if (layout != null)
                {
                    if (PDF.ValidateOutputFilePath())
                    {
                        await QueuedTask.Run(() => layout.Export(PDF));  //Export the layout to PDF on the worker thread
                    }
                    Debug.WriteLine("PDF file created!!");
                    return BA_ReturnCode.Success;
                }
                else
                {
                    MessageBox.Show("Could not find default layout. Map cannot be exported!!", "BAGIS PRO");
                    return BA_ReturnCode.UnknownError;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to export the map!! " + e.Message, "BAGIS PRO");
                return BA_ReturnCode.UnknownError;
            }

        }

        public static async Task GenerateMapsTitlePage()
        {
            try
            {
                // Query for the drainage area
                Uri gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true));
                string strAreaSqKm = await GeodatabaseTools.QueryTableForSingleValueAsync(gdbUri, Constants.FILE_SNOTEL_POURPOINT,
                                        Constants.FIELD_AOI_AREA, new QueryFilter());
                double areaSqKm = -1;
                bool isDouble = Double.TryParse(strAreaSqKm, out areaSqKm);

                //Query min/max from dem
                string sMask = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, true) + Constants.FILE_AOI_VECTOR;
                IList<double> lstResult = await GeoprocessingTools.GetDemStatsAsync(Module1.Current.Aoi.FilePath, sMask, 0.005);
                double elevMinMeters = -1;
                double elevMaxMeters = -1;
                if (lstResult.Count == 2)   // We expect the min and max values in that order
                {
                    elevMinMeters = lstResult[0];
                    elevMaxMeters = lstResult[1];
                }

                //Test querying metadata; Layer metadata methods not currently available in Pro
                //string snowCourseSitesPath = GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, true) +  Constants.FILE_SNOW_COURSE;
                //Item featureClassItem = ItemFactory.Instance.Create(snowCourseSitesPath);
                //IMetadata fcMetadataItem = featureClassItem as IMetadata;

                // Counting Snotel Sites in AOI boundary
                gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
                Uri sitesGdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Layers, false));
                int snotelInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_SNOTEL,
                    gdbUri, Constants.FILE_AOI_VECTOR);
                int snotelInBuffer = 0;
                int totalSnotelSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOTEL);
                if (totalSnotelSites > 0)
                {
                    snotelInBuffer = totalSnotelSites - snotelInBasin;
                }

                // Counting Snow Course Sites in AOI boundary
                int scosInBasin = await GeodatabaseTools.CountPointsWithinInFeatureAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE,
                    gdbUri, Constants.FILE_AOI_VECTOR);
                int scosInBuffer = 0;
                int totalScosSites = await GeodatabaseTools.CountFeaturesAsync(sitesGdbUri, Constants.FILE_SNOW_COURSE);
                if (totalScosSites > 0)
                {
                    scosInBuffer = totalScosSites - scosInBasin;
                }

                // Calculating percent represented area
                gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Aoi, false));
                double pctSnotelRepresented = 0;
                double pctSnowCourseRepresented = 0;
                double pctAllSitesRepresented = 0;
                double aoiArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_AOI_VECTOR);
                if (aoiArea > 0)
                {
                    gdbUri = new Uri(GeodatabaseTools.GetGeodatabasePath(Module1.Current.Aoi.FilePath, GeodatabaseNames.Analysis, false));
                    if (totalSnotelSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SNOTEL_REPRESENTED);
                        pctSnotelRepresented = Math.Round(repArea / aoiArea * 100);
                    }
                    if (totalScosSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SCOS_REPRESENTED);
                        pctSnowCourseRepresented = Math.Round(repArea / aoiArea * 100);
                    }
                    if (totalScosSites > 0 || totalScosSites > 0)
                    {
                        double repArea = await GeodatabaseTools.CalculateTotalPolygonAreaAsync(gdbUri, Constants.FILE_SITES_REPRESENTED);
                        pctAllSitesRepresented = Math.Round(repArea / aoiArea * 100);
                    }
                }

                // Serialize the title page object
                BA_Objects.ExportTitlePage tPage = new BA_Objects.ExportTitlePage
                {
                    aoi_name = Module1.Current.Aoi.Name,
                    comments = "This is a test",
                    publisher = "Lesley Bross",
                    local_path = Module1.Current.Aoi.FilePath,
                    streamgage_station = "USGS XXXXXXX",
                    drainage_area_sqkm = areaSqKm,
                    elevation_min_meters = elevMinMeters,
                    elevation_max_meters = elevMaxMeters,
                    snotel_sites_in_basin = snotelInBasin,
                    snotel_sites_in_buffer = snotelInBuffer,
                    snotel_sites_buffer_size = "???",
                    scos_sites_in_basin = scosInBasin,
                    scos_sites_in_buffer = scosInBuffer,
                    scos_sites_buffer_size = "???",
                    site_elev_range_ft = Module1.Current.Aoi.SiteElevRangeFeet,
                    site_buffer_dist_mi = Module1.Current.Aoi.SiteBufferDistMiles,
                    represented_snotel_percent = pctSnotelRepresented,
                    represented_snow_course_percent = pctSnowCourseRepresented,
                    represented_all_sites_percent = pctAllSitesRepresented,
                    date_created = DateTime.Now
                };
                string publishFolder = Module1.Current.Aoi.FilePath + "\\" + Constants.FOLDER_MAP_PACKAGE;
                string myXmlFile = publishFolder + "\\" + Constants.FILE_TITLE_PAGE_XML;
                System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(tPage.GetType());
                using (System.IO.FileStream fs = System.IO.File.Create(myXmlFile))
                {
                    writer.Serialize(fs, tPage);
                }

                // Process the title page through the xsl template
                string myStyleSheet = GeneralTools.GetAddInDirectory() + "\\" + Constants.FILE_TITLE_PAGE_XSL;
                XPathDocument myXPathDoc = new XPathDocument(myXmlFile);
                XslCompiledTransform myXslTrans = new XslCompiledTransform();
                myXslTrans.Load(myStyleSheet);
                string htmlFilePath = publishFolder + "\\" + Constants.FILE_TITLE_PAGE_HTML;
                using (XmlTextWriter myWriter = new XmlTextWriter(htmlFilePath, null))
                {
                    myXslTrans.Transform(myXPathDoc, null, myWriter);
                }

                // Convert the title page to PDF
                if (System.IO.File.Exists(htmlFilePath))
                {
                    PdfSharp.Pdf.PdfDocument titlePageDoc = TheArtOfDev.HtmlRenderer.PdfSharp.PdfGenerator.GeneratePdf(System.IO.File.ReadAllText(htmlFilePath),
                        PdfSharp.PageSize.Letter);
                    titlePageDoc.Save(publishFolder + "\\" + Constants.FILE_TITLE_PAGE_PDF);
                }
                Debug.WriteLine("Title page created!!");
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while trying to parse the XML!! " + e.Message, "BAGIS PRO");
            }
        }
    }

}