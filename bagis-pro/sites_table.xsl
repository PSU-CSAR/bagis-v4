﻿<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:output
     method="html"
     indent="yes"
     encoding="ISO-8859-1"/>

  <xsl:template match="/ExportTitlePage">
    <html xmlns="http://www.w3.org/1999/xhtml">

      <style type="text/css">
        .style1
        {
        height: 600px;
        }
        .style2
        {
        font-family: Arial, Helvetica, sans-serif;
        text-align: center;
        font-size: large;
        font-weight: bold;
        }
        .style3
        {
        font-family: Arial, Helvetica, sans-serif;
        padding: 1px 4px;
        border-style: solid;
        border-width: 1px;
        border-color:black;
        }
        .style4
        {
        font-family: Arial, Helvetica, sans-serif;
        font-size: 95%;
        padding-top: 1px;
        padding-left: 10px;
        }
        .footer {
        width: 100%;
        text-align: center;
        font-family: Arial, Helvetica, sans-serif;
        }

      </style>
  <head/>
  <body>
    <div class="style2">
      Active Sites in <xsl:value-of select="aoi_name"/>
    </div>
    <div class ="style1">
      <table style="border-collapse:collapse; width=600px">
        <tr>
          <td class="style3">
            Type
          </td>
          <td class="style3">
            Name
          </td>
          <td class="style3">
            Elevation
          </td>
          <td class="style3">
            Latitude
          </td>
          <td class="style3">
            Longitude
          </td>
        </tr>
        <xsl:for-each select="snotel_sites/Site">
            <tr>
              <td class="style3">
                <xsl:value-of select="SiteTypeText" />
              </td>
              <td class="style3">
                <xsl:value-of select="Name" />
              </td>
              <td class="style3">
                <xsl:value-of select="ElevationText" />
              </td>
              <td class="style3">
                <xsl:value-of select="LatitudeText" />
              </td>
              <td class="style3">
                <xsl:value-of select="LongitudeText" />
              </td>
            </tr>
        </xsl:for-each>
      </table>
    </div>
</body></html>
  </xsl:template>

</xsl:stylesheet>