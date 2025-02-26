﻿using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Windows;


namespace bagis_pro.Buttons
{
    internal class BtnDefineAoi : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("Define AOI!");
        }
    }
}
