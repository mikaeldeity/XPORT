﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RevitBatchExporter
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]

    class Export : IExternalCommand
    {
        internal static List<string> documents = new List<string>();

        internal static string destinationpath = "";

        internal static string splash = "Splash";
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;

            destinationpath = "";
            documents.Clear();

            string date = DateTime.Now.ToString("dd/MM/yyyy");

            int completed = 0;

            int failed = 0;

            var exportdialog = new RevitBatchExporter.Dialogs.ExportDialog();

            var dialog = exportdialog.ShowDialog();

            if (dialog != DialogResult.OK)
            {
                return Result.Cancelled;
            }

            WorksetConfiguration openConfig = new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets);
            OpenOptions openOptions = new OpenOptions();
            openOptions.SetOpenWorksetsConfiguration(openConfig);
            openOptions.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;

            SaveAsOptions wsaveAs = new SaveAsOptions();
            WorksharingSaveAsOptions saveConfig = new WorksharingSaveAsOptions();
            saveConfig.SaveAsCentral = true;
            wsaveAs.SetWorksharingOptions(saveConfig);
            wsaveAs.OverwriteExistingFile = true;

            SaveAsOptions saveAs = new SaveAsOptions();
            saveAs.OverwriteExistingFile = true;

            bool exportnwc = exportdialog.NWCCheckBox.Checked;

            bool exportifc = exportdialog.IFCCheckBox.Checked;


            if(exportifc | exportnwc)
            {
                openConfig = new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
                openOptions.SetOpenWorksetsConfiguration(openConfig);
            }

            string debugmessage = "";

            bool samepath = false;

            List<string[]> results = new List<string[]>();

            foreach(string path in documents)
            {
                string destdoc = nameprefix + Path.GetFileName(path.Replace(".rvt", "")) + namesuffix + ".rvt";

                if (File.Exists(destinationpath + destdoc))
                {
                    samepath = true;
                    break;
                }

                string pathOnly = Path.GetDirectoryName(path) + "\\";

                if(pathOnly == destinationpath)
                {
                    samepath = true;
                    break;
                }
            }

            if (samepath)
            {
                TaskDialog td = new TaskDialog("XPORT");
                td.MainInstruction = "Some documents already exist in the destination path.";
                td.MainContent = "The files will be overritten, do you wish to continue?";

                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Continue");
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Cancel");

                switch (td.Show())
                {
                    case TaskDialogResult.CommandLink1:
                        break;

                    case TaskDialogResult.CommandLink2:
                        return Result.Cancelled;

                    default:
                        return Result.Cancelled;
                }
            }            

            uiapp.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(OnDialogBoxShowing);
            uiapp.Application.FailuresProcessing += FailureProcessor;

            DateTime start = DateTime.Now;

            foreach (string path in documents)
            {
                string[] result = new string[6];

                if (!File.Exists(path))
                {
                    result[0] = Path.GetFileName(path.Replace(".rvt", ""));
                    result[1] = "false";
                    result[2] = "false";
                    result[3] = "false";
                    result[4] = "File Not Found";
                    result[5] = "";
                    results.Add(result);
                    failed++;
                    continue;
                }

                try
                {
                    DateTime s1 = DateTime.Now;

                    Document doc = uiapp.Application.OpenDocumentFile(ModelPathUtils.ConvertUserVisiblePathToModelPath(path), openOptions);

                    Transaction t1 = new Transaction(doc, "XP");

                    t1.Start();

                    if(customdate != "")
                    {
                        date = customdate;
                    }

                    try
                    {
                        doc.ProjectInformation.LookupParameter("Model Issue Date").Set(date);
                    }
                    catch { };

                    try
                    {
                        doc.ProjectInformation.IssueDate = date;
                    }
                    catch { };

                    if(reason != "")
                    {
                        try
                        {
                            doc.ProjectInformation.LookupParameter("Model Issue Reason").Set(reason);
                        }
                        catch { }
                    }
                                

                    t1.Commit();

                    string docname = doc.Title;                    

                    docname = docname.Replace("_detached", "");

                    if (docname.EndsWith(".rvt"))
                    {
                        docname = docname.Replace(".rvt", "");
                    }

                    if (nameprefix != "")
                    {
                        docname = nameprefix + docname;
                    }

                    if (namesuffix != "")
                    {
                        docname = docname + namesuffix;
                    }

                    bool rvtexported = false;

                    bool nwcexported = false;

                    bool ifcexported = false;                    

                    if (exportnwc) { nwcexported = ExportNWC(doc, destinationpath, docname); }
                    if (exportifc) { ifcexported = ExportIFC(doc, destinationpath, docname); }

                    if (exportrvt)
                    {
                        try
                        {
                            if (doc.IsWorkshared)
                            {
                                doc.SaveAs(destinationpath + docname + ".rvt", wsaveAs);
                                doc.Close(false);
                            }
                            else
                            {
                                doc.SaveAs(destinationpath + docname + ".rvt", saveAs);
                                doc.Close(false);
                            }
                            rvtexported = true;
                        }
                        catch { doc.Close(false); }
                    }
                    else
                    {
                        doc.Close(false);
                    }                    

                    try
                    {
                        Directory.Delete(destinationpath + docname + "_backup", true);
                    }
                    catch { }

                    try
                    {
                        Directory.Delete(destinationpath + "Revit_temp",true);
                    }
                    catch { }
                    
                    doc.Dispose();
                    completed++;

                    DateTime e1 = DateTime.Now;

                    int h = (e1 - s1).Hours;

                    int m = (e1 - s1).Minutes;

                    int s = (e1 - s1).Seconds;


                    result[0] = Path.GetFileName(path.Replace(".rvt", ""));
                    result[1] = rvtexported.ToString();
                    result[2] = nwcexported.ToString();
                    result[3] = ifcexported.ToString();
                    result[4] = "Completed";
                    result[5] = h.ToString() + ":" + m.ToString() + ":" + s.ToString();

                    results.Add(result);
                }
                catch (Exception e)
                {
                    debugmessage = "\n" + "\n" + e.Message;
                    result[0] = Path.GetFileName(path.Replace(".rvt", ""));
                    result[1] = "false";
                    result[2] = "false";
                    result[3] = "false";
                    result[4] = "Failed";
                    result[5] = "";

                    results.Add(result);
                    failed++;
                }                
            }

            uiapp.DialogBoxShowing -= OnDialogBoxShowing;
            uiapp.Application.FailuresProcessing -= FailureProcessor;

            DateTime end = DateTime.Now;

            int hours = (end - start).Hours;

            int minutes = (end - start).Minutes;

            int seconds = (end - start).Seconds;            

            TaskDialog rd = new TaskDialog("Revit Batch Exporter");
            rd.MainInstruction = "Results";
            rd.MainContent = "Exported to: " + destinationpath + "\n" + "Completed: " + completed.ToString() + "\nFailed: " + failed.ToString() + "\nTotal Time: " + hours.ToString() + " h " + minutes.ToString() + " m " + seconds.ToString() + " s";

            rd.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Close");
            rd.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Show Details");

            documents.Clear();

            destinationpath = "";

            switch (rd.Show())
            {
                case TaskDialogResult.CommandLink1:
                    return Result.Succeeded;

                case TaskDialogResult.CommandLink2:

                    var resultsdialog = new RevitBatchExporter.Dialogs.ResultsDialog();                 

                    foreach (string[] r in results)
                    {
                        var item = new ListViewItem(r);
                        resultsdialog.ResultsView.Items.Add(item);
                    }

                    var rdialog = resultsdialog.ShowDialog();

                    return Result.Succeeded;

                default:
                    return Result.Succeeded;
            }
        }
        //private void DeleteRVTLinks(Document doc)
        //{
        //    var collector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks).ToElementIds();

        //    if (collector.Count != 0)
        //    {
        //        foreach (ElementId id in collector)
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //    }            
        //}
        //private void DeleteCADLinks(Document doc)
        //{
        //    var collector = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).ToElementIds();

        //    if (collector.Count != 0)
        //    {
        //        foreach (ElementId id in collector)
        //        {
        //            ImportInstance cad = doc.GetElement(id) as ImportInstance;

        //            if (cad.IsLinked)
        //            {
        //                try
        //                {
        //                    doc.Delete(id);
        //                }
        //                catch { }
        //            }
        //        }
        //    }            
        //}
        //private void DeleteCADImports(Document doc)
        //{
        //    var collector = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).ToElementIds();

        //    if (collector.Count != 0)
        //    {
        //        foreach (ElementId id in collector)
        //        {
        //            ImportInstance cad = doc.GetElement(id) as ImportInstance;

        //            if (!cad.IsLinked)
        //            {
        //                try
        //                {
        //                    doc.Delete(id);
        //                }
        //                catch { }
        //            }                    
        //        }
        //    }
        //}
        //private void DeleteViewsNotOnSheets(Document doc)
        //{
        //    var sheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets).ToElementIds();

        //    List<ElementId> viewsONsheets = new List<ElementId>();

        //    //get views on sheets
        //    if (sheets.Count != 0)
        //    {
        //        foreach (ElementId id in sheets)
        //        {
        //            ViewSheet sheet = doc.GetElement(id) as ViewSheet;

        //            viewsONsheets.AddRange(sheet.GetAllPlacedViews());
        //        }
        //    }

        //    List<ElementId> usedtemplates = new List<ElementId>();

        //    //get used templates
        //    foreach (ElementId id in viewsONsheets)
        //    {
        //        Autodesk.Revit.DB.View view = doc.GetElement(id) as Autodesk.Revit.DB.View;

        //        if (view.ViewTemplateId != ElementId.InvalidElementId)
        //        {
        //            if (!usedtemplates.Contains(id))
        //            {
        //                usedtemplates.Add(view.ViewTemplateId);
        //            }
        //        }
        //    }

        //    ICollection<ElementId> viewsNOTsheets = null;

        //    //if no views on sheets collect differently
        //    if (viewsONsheets.Count != 0)
        //    {
        //        viewsNOTsheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views).Excluding(viewsONsheets).ToElementIds();
        //    }
        //    else
        //    {
        //        viewsNOTsheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views).ToElementIds();
        //    }

        //    //if no views not on sheets return
        //    if (viewsNOTsheets.Count == 0) { return; }

        //    //delete views not on sheets and unused templates skip views with dependancy
        //    foreach (ElementId id in viewsNOTsheets)
        //    {
        //        Autodesk.Revit.DB.View view = doc.GetElement(id) as Autodesk.Revit.DB.View;

        //        if (!view.IsTemplate && view.GetDependentViewIds().Count == 0)
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //        else if (view.IsTemplate && !usedtemplates.Contains(id))
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //    }

        //    //get remaining views
        //    ICollection<ElementId> remainingviews;

        //    if (viewsONsheets.Count != 0)
        //    {
        //        remainingviews = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views).Excluding(viewsONsheets).ToElementIds();
        //    }
        //    else
        //    {
        //        remainingviews = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views).ToElementIds();
        //    }

        //    //delete views without dependent views
        //    foreach (ElementId id in remainingviews)
        //    {
        //        Autodesk.Revit.DB.View view = doc.GetElement(id) as Autodesk.Revit.DB.View;
        //        if (!view.IsTemplate && view.GetDependentViewIds().Count == 0)
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //    }
        //}
        //private void DeleteViewsONSheets(Document doc)
        //{
        //    var sheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets).ToElementIds();

        //    if (sheets.Count == 0) { return; }

        //    List<ElementId> viewsONsheets = new List<ElementId>();

        //    foreach (ElementId id in sheets)
        //    {
        //        ViewSheet sheet = doc.GetElement(id) as ViewSheet;

        //        viewsONsheets.AddRange(sheet.GetAllPlacedViews());
        //    }

        //    if(viewsONsheets.Count == 0) { return; }

        //    foreach (ElementId id in viewsONsheets)
        //    {
        //        Autodesk.Revit.DB.View view = doc.GetElement(id) as Autodesk.Revit.DB.View;

        //        if (!view.IsTemplate)
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }                                      
        //        }
        //    }

        //    var views = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views).ToElementIds();

        //    if(views.Count == 0) { return; }

        //    List<ElementId> usedtemplates = new List<ElementId>();

        //    foreach (ElementId id in views)
        //    {
        //        Autodesk.Revit.DB.View view = doc.GetElement(id) as Autodesk.Revit.DB.View;

        //        if (view.ViewTemplateId != ElementId.InvalidElementId)
        //        {
        //            if (!usedtemplates.Contains(id))
        //            {
        //                usedtemplates.Add(view.ViewTemplateId);
        //            }
        //        }
        //    }

        //    foreach (ElementId id in views)
        //    {
        //        Autodesk.Revit.DB.View view = doc.GetElement(id) as Autodesk.Revit.DB.View;

        //        if (view.IsTemplate && !usedtemplates.Contains(id))
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //    }
        //}
        //private void DeleteSheets(Document doc)
        //{
        //    var sheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets).ToElementIds();

        //    if (sheets.Count != 0)
        //    {
        //        foreach (ElementId id in sheets)
        //        {
        //            if (!doc.GetElement(id).Name.Contains(splash))
        //            {
        //                try
        //                {
        //                    doc.Delete(id);
        //                }
        //                catch { }
        //            }
        //        }
        //    }            
        //}
        //private void DeleteAllViewsSheets(Document doc)
        //{            
        //    var views = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views).ToElementIds();

        //    if (views.Count != 0)
        //    {
        //        foreach (ElementId id in views)
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //    }            

        //    var sheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets).ToElementIds();

        //    if (sheets.Count != 0)
        //    {
        //        foreach (ElementId id in sheets)
        //        {
        //            if (!doc.GetElement(id).Name.Contains(splash))
        //            {
        //                try
        //                {
        //                    doc.Delete(id);
        //                }
        //                catch { }
        //            }
        //        }
        //    }            
        //}
        //private void DeleteSchedules(Document doc)
        //{
        //    var collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).ToElementIds();

        //    if (collector.Count != 0)
        //    {
        //        foreach (ElementId id in collector)
        //        {
        //            try
        //            {
        //                doc.Delete(id);
        //            }
        //            catch { }
        //        }
        //    }            
        //}
        //private void UngroupGroups(Document doc)
        //{
        //    var collector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups).WhereElementIsNotElementType().ToElements();

        //    if (collector.Count == 0)
        //    {
        //        return;
        //    }

        //    foreach (Group g in collector)
        //    {
        //        try
        //        {
        //            g.UngroupMembers();
        //        }
        //        catch { }
        //    }

        //    var groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups).ToElementIds();

        //    foreach (ElementId id in groups)
        //    {
        //        try
        //        {
        //            doc.Delete(id);
        //        }
        //        catch { }
        //    }
        //}
        //private void PurgeDocument(Document doc)
        //{
        //    Guid guid = new Guid("e8c63650-70b7-435a-9010-ec97660c1bda");

        //    var performanceAdviser = PerformanceAdviser.GetPerformanceAdviser();

        //    List<PerformanceAdviserRuleId> ruleId = new List<PerformanceAdviserRuleId>();

        //    var allRuleIds = performanceAdviser.GetAllRuleIds();

        //    foreach (var r in allRuleIds)
        //    {
        //        if (r.Guid == guid)
        //        {
        //            ruleId.Add(r);
        //        }
        //    }

        //    IList<PerformanceAdviserRuleId> ruleIds = ruleId;

        //    var failureMessages = performanceAdviser.ExecuteRules(doc, ruleId);

        //    if (failureMessages.Count > 0)
        //    {
        //        var purgableElementIds = failureMessages[0].GetFailingElements();

        //        try
        //        {
        //            doc.Delete(purgableElementIds);
        //        }
        //        catch
        //        {
        //            foreach(ElementId id in purgableElementIds)
        //            {
        //                doc.Delete(id);
        //            }
        //        }
        //    }
        //}
        private bool ExportIFC(Document doc,string folder,string name)
        {
            IFCExportOptions ifcoptions = new IFCExportOptions();
            try
            {
                doc.Export(folder, name, ifcoptions);
                return true;
            }
            catch { return false; }
        }
        private bool ExportNWC(Document doc, string folder, string name)
        {
            NavisworksExportOptions navisoptions = new NavisworksExportOptions();
            navisoptions.ExportLinks = false;
            navisoptions.ConvertElementProperties = true;
            navisoptions.FindMissingMaterials = true;
            navisoptions.Coordinates = NavisworksCoordinates.Shared;

            ViewFamilyType viewFamilyType3D = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault<ViewFamilyType>(x => ViewFamily.ThreeDimensional == x.ViewFamily);

            Transaction t1 = new Transaction(doc, "Create View");

            t1.Start();

            View3D temp3Dview = View3D.CreateIsometric(doc, viewFamilyType3D.Id);

            List<PhaseFilter> phaseFilters = new FilteredElementCollector(doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>().ToList();
            foreach(PhaseFilter p in phaseFilters)
            {
                if(p.Name == "None")
                {
                    temp3Dview.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER).Set(p.Id);
                }
            }

            navisoptions.ViewId = temp3Dview.Id;

            t1.Commit();

            try
            {                
                doc.Export(folder, name, navisoptions);
                t1.Start();
                doc.Delete(temp3Dview.Id);
                t1.Commit();
                return true;
            }
            catch (Exception e)
            {               
                string message = e.Message;
                t1.Start();
                doc.Delete(temp3Dview.Id);
                t1.Commit();
                return false;
            }
        }
        private bool ExportDWG(View3D view, string folder)
        {

            return false;
        }
        private void FailureProcessor(object sender, FailuresProcessingEventArgs e)
        {
            FailuresAccessor fas = e.GetFailuresAccessor();

            List<FailureMessageAccessor> fma = fas.GetFailureMessages().ToList();

            foreach (FailureMessageAccessor fa in fma)
            {
                string failuremessage = fa.GetDescriptionText();

                fas.DeleteWarning(fa);
            }
        }
        private void OnDialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            if (e is TaskDialogShowingEventArgs e1)
            {
                e1.OverrideResult((int)TaskDialogResult.CommandLink1);
            }

        }
    }
}
