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
using ArcGIS.Desktop.Mapping;

namespace BAGIS_V4.Menus
{
    internal class MnuBasinAnalyst_AddRefLayers : Button
    {
        protected override void OnClick()
        {
            Module1.ToggleState("MnuBasinAnalyst_BasinInfo");
            MessageBox.Show("Add reference layers");
        }
    }

    internal class MnuBasinAnalyst_SaveMxd : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("Save AOI MXD");
        }
    }

    internal class MnuBasinAnalyst_BasinInfo : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("Basin info");
        }
    }

    internal class MnuBasinAnalyst_AoiUtilities : Button
    {
        protected override void OnClick()
        {
            MessageBox.Show("AOI Utilities");
        }
    }
}
