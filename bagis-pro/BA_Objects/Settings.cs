﻿using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro.BA_Objects
{
    class Settings
    {
        public string m_pourpointUri = "https://services.arcgis.com/SXbDpmb7xQkk44JV/arcgis/rest/services/stations_USGS_ACTIVE/FeatureServer/0";
        public string m_nameField = "name";
        public string m_snodasSweUri = "http://bagis.geog.pdx.edu/arcgis/services/DAILY_SWE_NORMALS/";
        public IDictionary<string, DataSource> m_dataSources;


        public Settings()
        {
 

        }


    }

    class DataSource
    {
        public string layerType;
        public string description;
        public string uri;
        public DateTime dateClipped;
    }
}
