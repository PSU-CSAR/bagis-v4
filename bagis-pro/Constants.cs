﻿using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    static class Constants
    {
        public const string MAPS_DEFAULT_LAYOUT_NAME = "Basin Analysis Layout";
        public const string MAPS_DEFAULT_MAP_FRAME_NAME = "Basin Analysis Map Frame";
        public const string MAPS_DEFAULT_MAP_NAME = "Basin Analysis";
        public const string MAPS_AOI_BOUNDARY = "AOI Boundary";
        public const string MAPS_STREAMS = "AOI Streams";
        public const string MAPS_SNOTEL = "SNOTEL Sites";
        public const string MAPS_SNOW_COURSE = "Snow Courses";
        public const string MAPS_HILLSHADE = "hillshade";
        public const string MAPS_ELEV_ZONE = "Elevation Zones";
        public const string MAPS_SLOPE_ZONE = "Slope Zones";
        public const string MAPS_ASPECT_ZONE = "Aspect Zones";
        public const string MAPS_SNOTEL_REPRESENTED = "SNOTEL Sites Represented Area";
        public const string MAPS_SNOW_COURSE_REPRESENTED = "Snow Course Sites Represented Area";
        public const string MAPS_ALL_SITES_REPRESENTED = "All Sites Represented Area";

        public const string MAPS_LEGEND = "Legend";
        public const string MAPS_TITLE = "Title";
        public const string MAPS_SUBTITLE = "SubTitle";
        public const string MAPS_TEXTBOX1 = "TextBox1";

        public const string FILE_AOI_VECTOR = "aoi_v";
        public const string FILE_AOI_RASTER = "aoibagis";
        public const string FILE_STREAMS = "aoi_streams";
        public const string FILE_SNOTEL = "snotel_sites";
        public const string FILE_SNOW_COURSE = "snowcourse_sites";
        public const string FILE_HILLSHADE = "hillshade";
        public const string FILE_DEM_FILLED = "dem_filled";
        public const string FILE_ELEV_ZONE = "elevzone";
        public const string FILE_SLOPE_ZONE = "slpzone";
        public const string FILE_ASPECT_ZONE = "aspzone";
        public const string FILE_SNOTEL_REPRESENTED = "npactual";
        public const string FILE_SCOS_REPRESENTED = "nppseduo";
        public const string FILE_SITES_REPRESENTED = "npdiff";
        public const string FILE_SNOTEL_POURPOINT = "pourpoint";
        public const string FILE_EXPORT_MAP_ELEV_PDF = "map_elevation.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAP_SCOS_PDF = "map_elevation_sc.pdf";
        public const string FILE_EXPORT_MAP_SNOTEL_AND_SCOS_PDF = "map_elevation_snotel_sc.pdf";
        public const string FILE_EXPORT_MAP_PRECIPITATION_PDF = "map_precipitation.pdf";
        public const string FILE_EXPORT_MAP_SNODAS_PDF = "map_snodas.pdf";
        public const string FILE_EXPORT_MAP_ASPECT_PDF = "map_aspect.pdf";
        public const string FILE_EXPORT_MAP_SLOPE_PDF = "map_slope.pdf";
        public const string FILE_EXPORT_MAP_ELEVATION_SNOTEL_PDF = "map_elevation_snotel.pdf";
        public const string FILE_EXPORT_MAPS_ALL_PDF = "all_maps_charts.pdf";
        public const string FILE_TITLE_PAGE_XSL = "title_page.xsl";
        public const string FILE_TITLE_PAGE_XML = "title_page.xml";
        public const string FILE_TITLE_PAGE_HTML = "title_page.html";
        public const string FILE_TITLE_PAGE_PDF = "title_page.pdf";

        public const string FOLDER_MAP_PACKAGE = "maps_publish";

        public const string FIELD_AOI_AREA = "AOISHPAREA";
        public const string FIELD_SITE_ELEV = "BA_SELEV";
        public const string FIELD_SITE_NAME = "BA_SNAME";
        public const string FIELD_OBJECT_ID = "OBJECTID";

        public const int VALUE_NO_DATA_9999 = -9999;
        public const string VALUE_NO_DATA = "NoData";
    }

}
