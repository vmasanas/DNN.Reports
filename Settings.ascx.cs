#region Copyright

// 
// DotNetNuke® - http://www.dotnetnuke.com
// Copyright (c) 2002-2018
// by DotNetNuke Corporation
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
//

#endregion


namespace DotNetNuke.Modules.Reports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Web;
    using System.Web.Configuration;
    using System.Web.UI;
    using System.Web.UI.WebControls;
    using Components;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Modules.Reports.Converters;
    using DotNetNuke.Modules.Reports.DataSources;
    using DotNetNuke.Modules.Reports.Exceptions;
    using DotNetNuke.Modules.Reports.Extensions;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    /// -----------------------------------------------------------------------------
    /// <summary>
    ///     The Settings class manages Module Settings
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <history>
    /// </history>
    /// -----------------------------------------------------------------------------
    [DNNtc.ModuleControlProperties("Settings", "Reports Settings", DNNtc.ControlType.Edit, "", false, false)]
    public partial class Settings : ModuleSettingsBase
    {
        #region  Properties

        private ReportInfo Report
        {
            get { return this.ViewState["Report"] as ReportInfo; }
            set { this.ViewState["Report"] = value; }
        }

        #endregion

        /// -----------------------------------------------------------------------------
        /// <summary>
        ///     LoadSettings loads the settings from the Database and displays them
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <history>
        /// </history>
        /// -----------------------------------------------------------------------------
        public override void LoadSettings()
        {
            if (!this.Page.IsPostBack)
            {
                this.Report = ReportsController.GetReport(this.ModuleConfiguration);
                if (ReferenceEquals(this.Report, null))
                {
                    this.Report = new ReportInfo();
                }

                var reportsModuleSettingsRepository = new ReportsModuleSettingsRepository();
                var reportsModuleSettings = reportsModuleSettingsRepository.GetSettings(this.ModuleConfiguration);

                // If the user has permission to see the Data Source Settings
                if (this.CheckPermissions())
                {
                    // Load the fields
                    this.txtTitle.Text = reportsModuleSettings.Title;
                    this.txtDescription.Text = reportsModuleSettings.Description;
                    this.txtParameters.Text = reportsModuleSettings.Parameters;

                    // Load the Data Source Settings
                    var temp_extensionName = reportsModuleSettings.DataSource;
                    this.LoadExtensionSettings("DataSource", ref temp_extensionName, "DataSourceName.Text",
                                               "DataSource.Text", ReportsConstants.DEFAULT_DataSource,
                                               this.DataSourceDropDown,
                                               this.DataSourceSettings, this.DataSourceNotConfiguredView,
                                               this.Report.DataSourceSettings,
                                               ReportsConstants.FILENAME_RESX_DataSource, true);
                    this.Report.DataSource = temp_extensionName;

                    // Load the filtering settings
                    var encodeBuilder = new StringBuilder();
                    var decodeBuilder = new StringBuilder();
                    foreach (List<ConverterInstanceInfo> list in this.Report.Converters.Values)
                    {
                        foreach (var Converter in list)
                        {
                            StringBuilder builder = null;
                            if ("HtmlEncode".Equals(Converter.ConverterName))
                            {
                                builder = encodeBuilder;
                            }
                            else if ("HtmlDecode".Equals(Converter.ConverterName))
                            {
                                builder = decodeBuilder;
                            }

                            if (builder != null)
                            {
                                if (builder.Length > 0)
                                {
                                    builder.Append(",");
                                }
                                builder.Append(Converter.FieldName);
                            }
                        }
                    }
                    this.txtHtmlEncode.Text = encodeBuilder.ToString();
                    this.txtHtmlDecode.Text = decodeBuilder.ToString();
                }

                this.txtCacheDuration.Text = reportsModuleSettings.CacheDuration.ToString();
                this.chkShowInfoPane.Checked = reportsModuleSettings.ShowInfoPane;
                this.chkShowControls.Checked = reportsModuleSettings.ShowControls;
                this.chkAutoRunReport.Checked = reportsModuleSettings.AutoRunReport;
				this.chkExportExcel.Checked = reportsModuleSettings.ExportExcel;
				this.chkTokenReplace.Checked = reportsModuleSettings.TokenReplace;

                // Set the caching checkbox
                if (reportsModuleSettings.CacheDuration <= 0)
                {
                    this.chkCaching.Checked = false;
                    this.Report.CacheDuration = 0;
                }
                else
                {
                    this.chkCaching.Checked = true;
                }

                // Update the cache duration text box visibility
                this.UpdateCachingSpan();

                // Load Visualizer Settings
                var temp_extensionName2 = reportsModuleSettings.Visualizer;
                this.LoadExtensionSettings("Visualizer", ref temp_extensionName2, "VisualizerName.Text",
                                           "Visualizer.Text", ReportsConstants.DEFAULT_Visualizer,
                                           this.VisualizerDropDown,
                                           this.VisualizerSettings, null, this.Report.VisualizerSettings,
                                           ReportsConstants.FILENAME_RESX_Visualizer, false);
                this.Report.Visualizer = temp_extensionName2;
            }
        }

        /// -----------------------------------------------------------------------------
        /// <summary>
        ///     UpdateSettings saves the modified settings to the Database
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <history>
        /// </history>
        /// -----------------------------------------------------------------------------
        public override void UpdateSettings()
        {
            // Do not update report definition if the user is not a SuperUser
            if (this.UserInfo.IsSuperUser)
            {
                // Update the settings
                this.UpdateDataSourceSettings();

                // Save the report definition
                ReportsController.UpdateReportDefinition(this.ModuleId, this.Report);
            }

            // Non-SuperUsers can change TabModuleSettings (display settings)

            // Update cache duration (0 => no caching)
            var duration = "0";
            if (this.chkCaching.Checked)
            {
                duration = this.txtCacheDuration.Text;
            }

            this.Report.CacheDuration = int.Parse(duration);
            this.Report.ShowInfoPane = this.chkShowInfoPane.Checked;
            this.Report.ShowControls = this.chkShowControls.Checked;
            this.Report.AutoRunReport = this.chkAutoRunReport.Checked;
			this.Report.ExportExcel = this.chkExportExcel.Checked;
			this.Report.TokenReplace = this.chkTokenReplace.Checked;

            // and Visualizer Settings
            this.Report.Visualizer = this.VisualizerDropDown.SelectedValue;
            this.Report.VisualizerSettings.Clear();
            var settings = this.GetSettingsControlFromView(this.VisualizerSettings.GetActiveView());
            if (settings != null)
            {
                settings.SaveSettings(this.Report.VisualizerSettings);
            }

            // Save the report view and clear the cache
            ReportsController.UpdateReportView(this.TabModuleId, this.Report);

            // refresh cache
            ModuleController.SynchronizeModule(this.ModuleId);
            ReportsController.ClearCachedResults(this.ModuleId);
        }

        #region  Event Handlers

        protected void Page_Init(object sender, EventArgs e)
        {
            // Setup the extension lists
            this.VisualizerDropDown.Items.Clear();
            this.BuildExtensionList("Visualizer", ReportsConstants.FILENAME_RESX_Visualizer, "VisualizerName.Text",
                                    "Visualizer.Text", this.VisualizerDropDown, this.VisualizerSettings, true, false);

            this.BuildExtensionList("DataSource", ReportsConstants.FILENAME_RESX_DataSource, "DataSourceName.Text",
                                    "DataSource.Text", this.DataSourceDropDown, this.DataSourceSettings, true, true);

            // Register Confirm Messages
            ClientAPI.AddButtonConfirm(this.btnTestDataSource,
                                       Localization.GetString("btnTestDataSource.Confirm", this.LocalResourceFile));
            ClientAPI.AddButtonConfirm(this.btnShowXml,
                                       Localization.GetString("btnTestDataSource.Confirm", this.LocalResourceFile));
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Add module.css because it isn't loaded by default here (since the current module
            // is "Admin/Modules"
            ClientResourceManager.RegisterStyleSheet(this.Page, this.ResolveUrl("module.css"));

            // Update the selected extension on postback
            if (this.IsPostBack)
            {
                this.DisplaySelectedExtension("Visualizer", this.VisualizerDropDown,
                                              this.VisualizerSettings, null, this.Report.VisualizerSettings);
                this.DisplaySelectedExtension("DataSource", this.DataSourceDropDown,
                                              this.DataSourceSettings, this.DataSourceNotConfiguredView,
                                              this.Report.DataSourceSettings);
                var haveDataSource = !string.IsNullOrEmpty(this.DataSourceDropDown.SelectedValue);
                this.btnTestDataSource.Visible = haveDataSource;
                this.btnShowXml.Visible = haveDataSource;

                if ("True".Equals(
                    WebConfigurationManager.AppSettings[ReportsConstants.APPSETTING_AllowCachingWithParameters]))
                {
                    this.CacheWarningLabel.Attributes["ResourceKey"] = "CacheWithParametersEnabled.Text";
                }
            }

            // Register ClientAPI Functionality
            if (!ReportsClientAPI.IsSupported)
            {
                this.chkCaching.AutoPostBack = true;
                this.chkCaching.CheckedChanged += this.chkCaching_CheckedChanged;
            }
            else
            {
                ReportsClientAPI.Import(this.Page);
                ReportsClientAPI.ShowHideByCheckBox(this.Page, this.chkCaching, this.spanCacheDuration);
            }

            // Perform a server-side update of the caching text box span
            this.UpdateCachingSpan();
        }

        private void chkCaching_CheckedChanged(object sender, EventArgs args)
        {
            this.UpdateCachingSpan();
        }

        protected void btnShowXml_Click(object sender, EventArgs e)
        {
            this.rowXmlSource.Visible = false;
            this.rowResults.Visible = false;

            try
            {
                // Update data source settings
                this.UpdateDataSourceSettings();

                // Execute the DataSource
                var results = ReportsController.ExecuteReport(this.Report, null,
                                                              true, this);

                // Serialize the results to Xml
                var writer = new StringWriter();
                results.WriteXml(writer);
                this.txtXmlSource.Text = writer.ToString();
                this.rowXmlSource.Visible = true;
            }
            catch (DataSourceException ex)
            {
                // Format an error message
                this.lblQueryResults.Text =
                    string.Format(Localization.GetString("TestDSFail.Message", this.LocalResourceFile),
                                  ex.LocalizedMessage);
                this.lblQueryResults.CssClass = "NormalRed";
                this.imgQueryResults.ImageUrl = "~/images/red-error.gif";
                this.rowResults.Visible = true;
                this.rowXmlSource.Visible = false;
            }
        }

        protected void btnHideXmlSource_Click(object sender, EventArgs e)
        {
            this.rowXmlSource.Visible = false;
        }

        protected void btnTestQuery_Click(object sender, EventArgs e)
        {
            this.rowXmlSource.Visible = false;
            this.rowResults.Visible = false;

            try
            {
                // Update data source settings
                this.UpdateDataSourceSettings();

                // Execute the DataSource
                var results = ReportsController.ExecuteReport(this.Report, null,
                                                              true, this);

                // Format a success message
                this.lblQueryResults.Text = string.Format(Localization.GetString("TestDSSuccess.Message",
                                                                                 this.LocalResourceFile),
                                                          results.Rows.Count);
                this.lblQueryResults.CssClass = "NormalBold";
                this.imgQueryResults.ImageUrl = "~/images/green-ok.gif";
            }
            catch (DataSourceException ex)
            {
                // Format an error message
                this.lblQueryResults.Text =
                    string.Format(Localization.GetString("TestDSFail.Message", this.LocalResourceFile),
                                  ex.LocalizedMessage);
                this.lblQueryResults.CssClass = "NormalRed";
                this.imgQueryResults.ImageUrl = "~/images/red-error.gif";
            }

            // Display the results/error message
            this.rowResults.Visible = true;
        }

        protected void btnHideTestResults_Click(object sender, EventArgs e)
        {
            this.rowResults.Visible = false;
        }

        #endregion

        #region  Private Methods

        private void UpdateDataSourceSettings()
        {
            // Load the data source settings into the report
            var security = new PortalSecurity();
            this.Report.Title = this.txtTitle.Text;
            this.Report.Description =
                security.InputFilter(this.txtDescription.Text, PortalSecurity.FilterFlag.NoScripting);
            this.Report.Parameters = this.txtParameters.Text;
            this.Report.CreatedBy = this.UserId;
            this.Report.CreatedOn = DateTime.Now;
            this.Report.DataSource = this.DataSourceDropDown.SelectedValue;

            // Get the active data source settings control
            var activeDataSource = this.GetSettingsControlFromView(this.DataSourceSettings.GetActiveView()) as
                                       IDataSourceSettingsControl;

            // If there is an active data source, save its settings
            if (activeDataSource != null)
            {
                this.Report.DataSourceClass = activeDataSource.DataSourceClass;
                activeDataSource.SaveSettings(this.Report.DataSourceSettings);
            }

            // Update Converter settings
            this.Report.Converters.Clear();
            if (!string.IsNullOrEmpty(this.txtHtmlDecode.Text.Trim()))
            {
                foreach (var field in this.txtHtmlDecode.Text.Split(','))
                {
                    var newConverter = new ConverterInstanceInfo();
                    newConverter.FieldName = Convert.ToString(field);
                    newConverter.ConverterName = "HtmlDecode";
                    newConverter.Arguments = null;
                    ConverterUtils.AddConverter(this.Report.Converters, newConverter);
                }
            }

            if (!string.IsNullOrEmpty(this.txtHtmlEncode.Text.Trim()))
            {
                foreach (var field in this.txtHtmlEncode.Text.Split(','))
                {
                    var newConverter = new ConverterInstanceInfo();
                    newConverter.FieldName = Convert.ToString(field);
                    newConverter.ConverterName = "HtmlEncode";
                    newConverter.Arguments = null;
                    ConverterUtils.AddConverter(this.Report.Converters, newConverter);
                }
            }
        }

        private void LoadExtensionSettings(string extensionType, ref string extensionName, string nameResourceKey,
                                           string typeResourceKey, string defaultExtension, DropDownList dropDown,
                                           MultiView multiView, View notConfiguredView,
                                           Dictionary<string, string> extensionSettings, string resxFileName,
                                           bool buildNotSpecifiedItem)
        {
            // Build the list of Data Sources
            this.BuildExtensionList(extensionType, resxFileName, nameResourceKey,
                                    typeResourceKey, dropDown, multiView, true, buildNotSpecifiedItem);

            // Check that the Report has a Data Source, if not, use the default
            if (string.IsNullOrEmpty(extensionName))
            {
                extensionName = defaultExtension;
            }

            // Find that data source and select it
            var extensionItem = dropDown.Items.FindByValue(extensionName);
            if (extensionItem != null)
            {
                extensionItem.Selected = true;
                this.DisplaySelectedExtension(extensionType, dropDown, multiView, notConfiguredView, extensionSettings);
            }
        }

        private void DisplaySelectedExtension(string extensionType, DropDownList dropDown, MultiView multiView,
                                              View notConfiguredView, Dictionary<string, string> extensionSettings)
        {
            // Get the new active view name
            var newActiveViewName = Null.NullString;
            if (!string.IsNullOrEmpty(dropDown.SelectedValue))
            {
                newActiveViewName = this.GetExtensionViewName(dropDown.SelectedValue, extensionType);
            }

            // Get the current active view
            var activeView = multiView.GetActiveView();
            if (activeView != null &&
                activeView.ID.Equals(newActiveViewName, StringComparison.OrdinalIgnoreCase))
            {
                return; // If current and new active view are the same, just return
            }

            // Get the new active view
            View newActiveView = null;
            if (string.IsNullOrEmpty(newActiveViewName))
            {
                // If <No Visualizer/DataSource> is selected, use the not configured view
                newActiveView = notConfiguredView;
                if (ReferenceEquals(newActiveView, null))
                {
                    newActiveView = multiView.Views[0];
                }
            }
            else
            {
                // Otherwise, find the view with the new active view name
                newActiveView = (View) multiView.FindControl(newActiveViewName);
            }

            // Set that view as the active view
            multiView.SetActiveView(newActiveView);

            // Get the settings control in the new active view
            var settingsControl = this.GetSettingsControlFromView(newActiveView);

            // If we successfully got it, load its settings
            if (settingsControl != null)
            {
                settingsControl.LoadSettings(extensionSettings);
            }
        }

        private void BuildExtensionList(string extensionType, string resxFileName, string nameResourceKey,
                                        string typeResourceKey, DropDownList dropDown, MultiView multiView,
                                        bool buildView, bool buildNotSpecifiedItem)
        {
            // Map the root physical path by using this control's location
            var rootPhysicalPath = this.Server.MapPath(this.TemplateSourceDirectory);

            // Load all the Settings.ascx files for the Extension
            var extTypeFolder = string.Concat(extensionType, "s");
            var exts =
                Utilities.GetExtensions(rootPhysicalPath, extTypeFolder);

            // Build the Drop down list
            dropDown.Items.Clear();
            if (buildNotSpecifiedItem)
            {
                dropDown.Items.Add(new ListItem(
                                       Localization.GetString(string.Format("No{0}.Text", extensionType),
                                                              this.LocalResourceFile),
                                       string.Empty));
            }
            buildView = buildView && multiView.Views.Count <= 1;
            foreach (var ext in exts)
            {
                // Resolve the extension path
                var extPath = string.Format("{0}/{1}", extTypeFolder, ext);

                // Locate the Local Resource File
                var lrWeb = string.Format("{0}/{1}/{2}",
                                          extPath,
                                          Localization.LocalResourceDirectory,
                                          resxFileName);
                var localResourceFile = this.ResolveUrl(lrWeb);
                if (!File.Exists(this.Server.MapPath(localResourceFile)))
                {
                    continue;
                }

                // Locate the Settings control
                var ctrlPath = this.ResolveUrl(string.Format("{0}/{1}/{2}", extTypeFolder, ext, "Settings.ascx"));
                if (!File.Exists(this.Server.MapPath(ctrlPath)))
                {
                    continue;
                }

                // Get the name of the data source
                var extName = Localization.GetString(nameResourceKey, localResourceFile);

                // Construct the settings control
                var ctrl = default(Control);
                try
                {
                    ctrl = this.LoadControl(ctrlPath);
                }
                catch (HttpException)
                {
                    continue;
                }
                var rptExt = ctrl as IReportsExtension;
                var rptCtrl = ctrl as IReportsControl;

                // Validate implemented interfaces
                if (ReferenceEquals(rptCtrl, null))
                {
                    continue;
                }
                if (ReferenceEquals(rptExt, null))
                {
                    continue;
                }
                if (!(ctrl is IReportsSettingsControl))
                {
                    continue;
                }

                // Construct an extension context
                var ctxt = new ExtensionContext(this.TemplateSourceDirectory, extensionType, ext);

                // Set properties and initialize extension
                rptExt.Initialize(ctxt);
                ctrl.ID = this.GetExtensionControlName(ext, extensionType);
                rptCtrl.ParentModule = this;

                // Don't build the view unless we're asked to AND
                // the views haven't already been built
                if (buildView)
                {
                    // Create a View to hold the control
                    var view = new View();
                    view.ID = this.GetExtensionViewName(ext, extensionType);
                    view.Controls.Add(ctrl);

                    // Add the view to the multi view
                    multiView.Views.Add(view);
                }

                // Get the full text for the drop down list item
                var itemText = string.Format(Localization.GetString(typeResourceKey, this.LocalResourceFile), extName);

                // Create the dropdown list items
                dropDown.Items.Add(new ListItem(itemText, ext));
                //End Try
            }
        }

        private string GetExtensionControlName(string extension, string type)
        {
            return string.Format("{0}{1}Settings", Utilities.RemoveSpaces(extension), type);
        }

        private string GetExtensionViewName(string extension, string type)
        {
            return string.Format("{0}{1}SettingsView", Utilities.RemoveSpaces(extension), type);
        }

        private IReportsSettingsControl GetSettingsControlFromView(View view)
        {
            // Return the first control inside that view
            return view.Controls[0] as IReportsSettingsControl;
        }

        private bool CheckPermissions()
        {
            if (!this.UserInfo.IsSuperUser)
            {
                this.ReportsSettingsMultiView.SetActiveView(this.AccessDeniedView);
                return false;
            }
            this.ReportsSettingsMultiView.SetActiveView(this.SuperUserView);
            return true;
        }

        protected void VisualizerDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Report.Visualizer = this.VisualizerDropDown.SelectedValue;
            ReportsController.LoadExtensionSettings(this.ModuleSettings, this.TabModuleSettings, this.Report);
            this.DisplaySelectedExtension("Visualizer", this.VisualizerDropDown, this.VisualizerSettings, null,
                                          this.Report.VisualizerSettings);
        }

        protected void DataSourceDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Report.DataSource = this.DataSourceDropDown.SelectedValue;
            ReportsController.LoadExtensionSettings(this.ModuleSettings, this.TabModuleSettings, this.Report);
            this.DisplaySelectedExtension("DataSource", this.DataSourceDropDown, this.DataSourceSettings,
                                          this.DataSourceNotConfiguredView, this.Report.DataSourceSettings);
        }

        private void UpdateCachingSpan()
        {
            if (this.chkCaching.Checked)
            {
                this.spanCacheDuration.Style[HtmlTextWriterStyle.Display] = string.Empty;
            }
            else
            {
                this.spanCacheDuration.Style[HtmlTextWriterStyle.Display] = "none";
            }
        }

        #endregion
    }
}