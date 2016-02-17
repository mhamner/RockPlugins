﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.CheckIn;
using Rock.Web.UI.Controls;
using Rock.Web.Cache;
using System.Web.UI.WebControls;
using System.Web.UI;
using Rock.Model;
using System.Reflection;
using System.Web.UI.HtmlControls;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Rock.Data;

namespace RockWeb.Plugins.org_secc.FamilyCheckin
{
    [DisplayName("QuickCheckin")]
    [Category("Check-in")]
    [Description("QuickCheckin block for helping parents check in their family quickly.")]

    public partial class QuickCheckin : CheckInBlock
    {
        private List<GroupTypeCache> parentGroupTypesList;
        private GroupTypeCache currentParentGroupType;
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            RockPage.AddScriptLink("~/Scripts/CheckinClient/cordova-2.4.0.js", false);
            RockPage.AddScriptLink("~/Scripts/CheckinClient/ZebraPrint.js");

            //RockPage.AddScriptLink("~/Scripts/iscroll.js");
            //RockPage.AddScriptLink("~/Scripts/CheckinClient/checkin-core.js");

            if (!KioskCurrentlyActive)
            {
                NavigateToHomePage();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!Page.IsPostBack)
            {
                List<string> errors = new List<string>();
                string workflowActivity = GetAttributeValue("WorkflowActivity");
                bool test = ProcessActivity(workflowActivity, out errors);

                //Find the parent group types
                parentGroupTypesList = CurrentCheckInState.CheckIn.Families.Where(f => f.Selected).FirstOrDefault()
                    .People.SelectMany(p => p.GroupTypes).SelectMany(gt => gt.GroupType.ParentGroupTypes)
                    .Where(pgt => pgt.ChildGroupTypes.Count > 1).DistinctBy(pgt => pgt.Guid).ToList();
                Session["parentGroupTypesList"] = parentGroupTypesList;

                //Find the current parent group type
                foreach (var parentGroupType in parentGroupTypesList)
                {
                    if (CurrentCheckInState.CheckIn.Families.Where(f => f.Selected).FirstOrDefault()
                           .People.SelectMany(p => p.GroupTypes)
                           .Where(gt => gt.Selected == true && gt.GroupType.ParentGroupTypes.Contains(parentGroupType)).Count() > 0)
                    {
                        currentParentGroupType = parentGroupType;
                        break;
                    }
                }
                Session["currentParentGroupType"] = currentParentGroupType;

                Session["modalActive"] = false;
                Session["modalPerson"] = null;
                Session["modalSchedule"] = null;
            }

            parentGroupTypesList = (List<GroupTypeCache>)Session["parentGroupTypesList"];
            currentParentGroupType = (GroupTypeCache)Session["currentParentGroupType"];

            DisplayParentGroupTypes();
            DisplayPeople();

            if ((bool)Session["modalActive"])
            {
                ShowRoomChangeModal((Person)Session["modalPerson"], (CheckInSchedule)Session["modalSchedule"]);
            }


            //Exit page on completion
            if (Request["__EVENTARGUMENT"] != null && (string)Request["__EVENTTARGET"]== "btnCancel_Click")
            {
                NavigateToNextPage();
            }
        }

        private void DisplayParentGroupTypes()
        {
            foreach (var parentGroupType in parentGroupTypesList)
            {
                var btnParentGroupType = new BootstrapButton();

                if (parentGroupType.Guid == currentParentGroupType.Guid)
                {
                    btnParentGroupType.CssClass = "btn btn-primary btn-lg col-xs-6";
                    btnParentGroupType.Enabled = false;
                }
                else
                {
                    btnParentGroupType.CssClass = "btn btn-default btn-lg col-xs-6";
                    btnParentGroupType.Enabled = true;
                }
                btnParentGroupType.Text = parentGroupType.Name;
                btnParentGroupType.ID = parentGroupType.Guid.ToString();
                btnParentGroupType.Click += (s, e) => { ChangeParentGroupType(parentGroupType); };
                phParentGroupTypes.Controls.Add(btnParentGroupType);
            }
        }

        private void ChangeParentGroupType(GroupTypeCache parentGroupType)
        {
            currentParentGroupType = parentGroupType;
            Session["currentParentGroupType"] = currentParentGroupType;
            foreach (var control in phParentGroupTypes.Controls)
            {
                try
                {
                    BootstrapButton btnParentGroupType = (BootstrapButton)control;
                    if (btnParentGroupType.ID == currentParentGroupType.Guid.ToString())
                    {
                        btnParentGroupType.CssClass = "btn btn-primary btn-lg col-xs-6";
                        btnParentGroupType.Enabled = false;
                    }
                    else
                    {
                        btnParentGroupType.CssClass = "btn btn-default btn-lg col-xs-6";
                        btnParentGroupType.Enabled = true;
                    }
                }
                catch { }
            }
            phPeople.Controls.Clear();
            DisplayPeople();
        }

        private void DisplayPeople()
        {
            var people = CurrentCheckInState.CheckIn.Families.SelectMany(f => f.People);

            foreach (var person in people)
            {
                HtmlGenericControl hgcRow = new HtmlGenericControl("div");
                hgcRow.AddCssClass("row");
                phPeople.Controls.Add(hgcRow);
                DisplayPersonButton(person, hgcRow);
                DisplayPersonCheckinAreas(person.Person, hgcRow);
            }
        }

        private void DisplayPersonCheckinAreas(Person person, HtmlGenericControl hgcRow)
        {
            var personSchedules = CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true))
                .SelectMany(gt => gt.Groups).SelectMany(g => g.Locations).SelectMany(l => l.Schedules)
                .DistinctBy(s => s.Schedule.Guid);

            foreach (var schedule in personSchedules)
            {
                HtmlGenericControl hgcAreaRow = new HtmlGenericControl("div");
                hgcRow.AddCssClass("row col-xs-12");
                hgcRow.Controls.Add(hgcAreaRow);
                DisplayPersonSchedule(person, schedule, hgcAreaRow);
            }
        }

        private void DisplayPersonSchedule(Person person, CheckInSchedule schedule, HtmlGenericControl hgcAreaRow)
        {
            BootstrapButton btnSchedule = new BootstrapButton();
            btnSchedule.Text = schedule.Schedule.Name + "<br>(Select Room To Checkin)";
            hgcAreaRow.Controls.Add(btnSchedule);
            btnSchedule.CssClass = "btn btn-default btn-lg col-xs-8";
            btnSchedule.ID = person.Guid.ToString() + currentParentGroupType.Guid.ToString() + schedule.Schedule.Guid.ToString();
            btnSchedule.Click += (s, e) => { ShowRoomChangeModal(person, schedule); };

            var groupType = CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true))
                .FirstOrDefault(gt => gt.Selected && gt.Groups.SelectMany(g => g.Locations).SelectMany(l => l.Schedules.Where(s => s.Selected)).Select(s => s.Schedule.Guid).Contains(schedule.Schedule.Guid) == true);

            if (groupType != null)
            {
                var group = CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true && gt == groupType))
                .SelectMany(gt => gt.Groups).FirstOrDefault(g => g.Selected);

                if (group != null)
                {
                    var room = CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                        .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                        .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true && gt == groupType))
                        .SelectMany(gt => gt.Groups.Where(g => g.Selected && g.Group.Guid == group.Group.Guid))
                        .SelectMany(g => g.Locations).Where(l => l.Selected && l.Schedules.FirstOrDefault(s => s.Schedule.Guid == schedule.Schedule.Guid).Selected)
                        .FirstOrDefault();

                    if (room != null)
                    {
                        btnSchedule.CssClass = "btn btn-primary btn-lg col-xs-8";
                        btnSchedule.Text = "<b>" + schedule.Schedule.Name + "</b><br>" + groupType + ": " + group + " > " + room;
                    }
                }
            }
        }

        private void ShowRoomChangeModal(Person person, CheckInSchedule schedule)
        {
            mdChooseClass.Content.Controls.Clear();
            List<CheckInGroupType> groupTypes = GetGroupTypes(person, schedule);

            foreach (var groupType in groupTypes)
            {
                List<CheckInGroup> groups = GetGroups(person, schedule, groupType);

                foreach (var group in groups)
                {
                    List<CheckInLocation> rooms = GetLocations(person, schedule, groupType, group);

                    foreach (var room in rooms)
                    {
                        //Change room button
                        BootstrapButton btnRoom = new BootstrapButton();
                        btnRoom.ID = "c" + person.Guid.ToString() + schedule.Schedule.Guid.ToString() + room.Location.Guid.ToString();
                        btnRoom.Text = groupType.GroupType.Name + ": " + group.Group.Name + " > " +
                            room.Location.Name + " - Count: " + KioskLocationAttendance.Read(room.Location.Id).CurrentCount;
                        btnRoom.CssClass = "btn btn-default btn-lg col-xs-12";
                        btnRoom.Click += (s, e) =>
                        {
                            ChangeRoomSelection(person, schedule, groupType, group, room);
                            Session["modalActive"] = false;
                            mdChooseClass.Hide();
                            phPeople.Controls.Clear();
                            DisplayPeople();
                        };
                        mdChooseClass.Content.Controls.Add(btnRoom);
                    }
                }
            }
            BootstrapButton btnCancel = new BootstrapButton();
            btnCancel.ID = "c" + person.Guid.ToString() + schedule.Schedule.Guid.ToString();
            btnCancel.Text = "(Do not check in at "+ schedule.Schedule.Name +")";
            btnCancel.CssClass = "btn btn-danger btn-lg col-xs-12";
            btnCancel.Click += (s, e) => {
                ClearRoomSelection(person, schedule);
                Session["modalActive"] = false;
                mdChooseClass.Hide();
                phPeople.Controls.Clear();
                DisplayPeople();
            };

            mdChooseClass.Content.Controls.Add(btnCancel);
            mdChooseClass.Title = "Choose Class";
            mdChooseClass.CancelLinkVisible = false;
            mdChooseClass.Show();
            Session["modalActive"] = true;
            Session["modalPerson"] = person;
            Session["modalSchedule"] = schedule;
        }

        protected void CancelModal(object sender, EventArgs e)
        {
            Session["modalActive"] = false;
            mdChooseClass.Hide();
        }

        private void ChangeRoomSelection(Person person, CheckInSchedule schedule,
            CheckInGroupType groupType, CheckInGroup group, CheckInLocation room)
        {
            ClearRoomSelection(person, schedule);
            room.Selected = true;
            group.Selected = true;
            groupType.Selected = true;
            room.Schedules.Where(s => s.Schedule.Guid == schedule.Schedule.Guid).FirstOrDefault().Selected = true;
            SaveState();
        }



        private void ClearRoomSelection(Person person, CheckInSchedule schedule)
        {
            List<CheckInGroupType> groupTypes = GetGroupTypes(person, schedule);
            
            foreach (var groupType in groupTypes)
            {
                List<CheckInGroup> groups = GetGroups(person, schedule, groupType);

                foreach (var group in groups)
                {
                    List<CheckInLocation> rooms = GetLocations(person, schedule, groupType, group);

                    foreach (var room in rooms)
                    {
                        //Change scheduals in room to not selected
                        foreach (var roomSchedule in room.Schedules)
                        {
                            if (roomSchedule.Schedule.Guid == schedule.Schedule.Guid)
                            {
                                roomSchedule.Selected = false;
                            }
                        }
                        //Set location as not selected if no schedules selected
                        if (room.Schedules.Where(s => s.Selected == true).Count() == 0)
                        {
                            room.Selected = false;
                        }
                    }
                    //Set group as not selected if no locations selected
                    if (group.Locations.Where(l => l.Selected == true).Count() == 0)
                    {
                        group.Selected = false;
                    }
                }
                //Set group type as not selected if no groups selected
                if (groupType.Groups.Where(g => g.Selected == true).Count() == 0)
                {
                    groupType.Selected = false;
                }
            }
            SaveState();
        }

        private List<CheckInLocation> GetLocations(Person person, CheckInSchedule schedule, CheckInGroupType groupType, CheckInGroup group)
        {
            return CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                        .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                        .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true))
                        .Where(gt => gt.GroupType.Guid == groupType.GroupType.Guid)
                        .SelectMany(gt => gt.Groups)
                        .Where(g => g.Group.Guid == group.Group.Guid)
                        .SelectMany(g => g.Locations.Where(
                            l => l.Schedules.Where(
                                s => s.Schedule.Guid == schedule.Schedule.Guid).Count() != 0)).ToList();
        }

        private List<CheckInGroup> GetGroups(Person person, CheckInSchedule schedule, CheckInGroupType groupType)
        {
            return CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true))
                .Where(gt => gt.GroupType.Guid == groupType.GroupType.Guid)
                .SelectMany(gt => gt.Groups)
                .Where(g => g.Locations.Where(
                    l => l.Schedules.Where(
                        s => s.Schedule.Guid == schedule.Schedule.Guid).Count() != 0).Count() != 0).ToList();
        }

        private List<CheckInGroupType> GetGroupTypes(Person person, CheckInSchedule schedule)
        {
            return CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                .SelectMany(f => f.People.Where(p => p.Person.Guid == person.Guid))
                .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == true))
                .Where(gt => gt.Groups.Where(g => g.Locations.Where(
                    l => l.Schedules.Where(
                        s => s.Schedule.Guid == schedule.Schedule.Guid).Count() != 0).Count() != 0).Count() != 0).ToList();
        }

        private void DisplayPersonButton(CheckInPerson person, HtmlGenericControl hgcRow)
        {
            //Checkin Button
            var btnPerson = new BootstrapButton();
            btnPerson.DataLoadingText = "<img src = '" + person.Person.PhotoUrl + "' style = 'height:100px;'><br /> Please Wait...";
            btnPerson.Text = "<img src='" + person.Person.PhotoUrl + "' style='height:100px;'><br>" + person.Person.FullName;
            btnPerson.ID = person.Person.Guid.ToString();
            btnPerson.Click += (s, e) => { TogglePerson(person); };
            hgcRow.Controls.Add(btnPerson);
            if (person.Selected)
            {
            btnPerson.CssClass = "btn btn-success btn-lg col-xs-4";
            }
            else
            {
                btnPerson.CssClass = "btn btn-default btn-lg col-xs-4";
            }
        }

        private void TogglePerson(CheckInPerson person)
        {
            if (person.Selected)
                person.Selected = false;
            else
                person.Selected = true;

            SaveState();
            phPeople.Controls.Clear();
            DisplayPeople();
        }

        protected void btnCancel_Click(object sender, EventArgs e)
        {
            NavigateToNextPage();
        }

        protected void btnCheckin_Click(object sender, EventArgs e)
        {
            //Unselect all groups not in parent group
            var groupTypes = CurrentCheckInState.CheckIn.Families.Where(f => f.Selected)
                .SelectMany(f => f.People)
                .SelectMany(p => p.GroupTypes.Where(gt => gt.GroupType.ParentGroupTypes.Select(pgt => pgt.Guid).Contains(currentParentGroupType.Guid) == false));
            foreach (var groupType in groupTypes)
            {
                groupType.Selected = false;
            }
            maNotice.Show("Checking in, please wait.", ModalAlertType.Information);
            //Check-in and print tags.
            List<string> errors = new List<string>();
            bool test = ProcessActivity("Save Attendance", out errors);
            ProcessLabels();
        }

        private void ProcessLabels()
        {
            var printQueue = new Dictionary<string, StringBuilder>();
            foreach (var selectedFamily in CurrentCheckInState.CheckIn.Families.Where(p => p.Selected))
            {
                List<CheckInLabel> labels = new List<CheckInLabel>();
                List<CheckInPerson> selectedPeople = selectedFamily.People.Where(p => p.Selected).ToList();
                List<FamilyLabel> familyLabels = new List<FamilyLabel>();

                foreach (CheckInPerson selectedPerson in selectedPeople)
                {
                    foreach (var groupType in selectedPerson.GroupTypes.Where(gt => gt.Selected))
                    {
                        using (var rockContext = new RockContext())
                        {
                            foreach (var label in groupType.Labels)
                            {
                                var file = new BinaryFileService(rockContext).Get(label.FileGuid);
                                file.LoadAttributes(rockContext);
                                string isFamilyLabel = file.GetAttributeValue("IsFamilyLabel");
                                if (isFamilyLabel != "True")
                                {
                                    labels.Add(label);
                                }
                                else
                                {
                                    List<string> mergeCodes = file.GetAttributeValue("MergeCodes").TrimEnd('|').Split('|').ToList();
                                    FamilyLabel familyLabel = familyLabels.FirstOrDefault(fl => fl.FileGuid == label.FileGuid &&
                                                                                    fl.MergeFields.Count < mergeCodes.Count);
                                    if (familyLabel == null)
                                    {
                                        familyLabel = new FamilyLabel();
                                        familyLabel.FileGuid = label.FileGuid;
                                        familyLabel.LabelObj = label;
                                        foreach (var mergeCode in mergeCodes)
                                        {
                                            familyLabel.MergeKeys.Add(mergeCode.Split('^')[0]);
                                        }
                                        familyLabels.Add(familyLabel);
                                    }
                                    familyLabel.MergeFields.Add((selectedPerson.Person.Age.ToString() ?? "#") + "yr-" + selectedPerson.SecurityCode);
                                }
                            }
                        }
                    }
                }

                //Format all FamilyLabels and add to list of labels to print.
                foreach (FamilyLabel familyLabel in familyLabels)
                {
                    //create padding to clear unused merge fields
                    List<string> padding = Enumerable.Repeat(" ", familyLabel.MergeKeys.Count).ToList();
                    familyLabel.MergeFields.AddRange(padding);
                    for (int i = 0; i < familyLabel.MergeKeys.Count; i++)
                    {
                        familyLabel.LabelObj.MergeFields[familyLabel.MergeKeys[i]] = familyLabel.MergeFields[i];
                    }
                    labels.Add(familyLabel.LabelObj);
                }

                // Print client labels
                if (labels.Any(l => l.PrintFrom == Rock.Model.PrintFrom.Client))
                {
                    var clientLabels = labels.Where(l => l.PrintFrom == PrintFrom.Client).ToList();
                    var urlRoot = string.Format("{0}://{1}", Request.Url.Scheme, Request.Url.Authority);
                    clientLabels.ForEach(l => l.LabelFile = urlRoot + l.LabelFile);
                    AddLabelScript(clientLabels.ToJson());
                }

                // Print server labels
                if (labels.Any(l => l.PrintFrom == Rock.Model.PrintFrom.Server))
                {
                    string delayCut = @"^XB";
                    string endingTag = @"^XZ";
                    var printerIp = string.Empty;
                    var labelContent = new StringBuilder();

                    // make sure labels have a valid ip
                    var lastLabel = labels.Last();
                    foreach (var label in labels.Where(l => l.PrintFrom == PrintFrom.Server && !string.IsNullOrEmpty(l.PrinterAddress)))
                    {
                        var labelCache = KioskLabel.Read(label.FileGuid);
                        if (labelCache != null)
                        {
                            if (printerIp != label.PrinterAddress)
                            {
                                printQueue.AddOrReplace(label.PrinterAddress, labelContent);
                                printerIp = label.PrinterAddress;
                                labelContent = new StringBuilder();
                            }

                            var printContent = labelCache.FileContent;
                            foreach (var mergeField in label.MergeFields)
                            {
                                if (!string.IsNullOrWhiteSpace(mergeField.Value))
                                {
                                    printContent = Regex.Replace(printContent, string.Format(@"(?<=\^FD){0}(?=\^FS)", mergeField.Key), ZebraFormatString(mergeField.Value));
                                }
                                else
                                {
                                    printContent = Regex.Replace(printContent, string.Format(@"\^FO.*\^FS\s*(?=\^FT.*\^FD{0}\^FS)", mergeField.Key), string.Empty);
                                    printContent = Regex.Replace(printContent, string.Format(@"\^FD{0}\^FS", mergeField.Key), "^FD^FS");
                                }
                            }

                            // send a Delay Cut command at the end to prevent cutting intermediary labels
                            if (label != lastLabel)
                            {
                                printContent = Regex.Replace(printContent.Trim(), @"\" + endingTag + @"$", delayCut + endingTag);
                            }

                            labelContent.Append(printContent);
                        }
                    }

                    printQueue.AddOrReplace(printerIp, labelContent);
                }

                if (printQueue.Any())
                {
                    PrintLabels(printQueue);
                    printQueue.Clear();
                }
            }
        }

        /// <summary>
        /// Prints the labels.
        /// </summary>
        /// <param name="families">The families.</param>
        private void PrintLabels(Dictionary<string, StringBuilder> printerContent)
        {
            foreach (var printerIp in printerContent.Keys.Where(k => !string.IsNullOrEmpty(k)))
            {
                StringBuilder labelContent;
                if (printerContent.TryGetValue(printerIp, out labelContent))
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    var printerIpEndPoint = new IPEndPoint(IPAddress.Parse(printerIp), 9100);
                    var result = socket.BeginConnect(printerIpEndPoint, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(5000, true);

                    if (socket.Connected)
                    {
                        var ns = new NetworkStream(socket);
                        byte[] toSend = System.Text.Encoding.ASCII.GetBytes(labelContent.ToString());
                        ns.Write(toSend, 0, toSend.Length);
                    }
                    else
                    {
                        //phPrinterStatus.Controls.Add(new LiteralControl(string.Format("Can't connect to printer: {0}", printerIp)));
                    }

                    if (socket != null && socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Adds the label script.
        /// </summary>
        /// <param name="jsonObject">The json object.</param>
        private void AddLabelScript(string jsonObject)
        {
            string script = string.Format(@"
            var labelData = {0};
		    function printLabels() {{
		        ZebraPrintPlugin.printTags(
            	    JSON.stringify(labelData),
            	    function(result) {{
			        }},
			        function(error) {{
				        // error is an array where:
				        // error[0] is the error message
				        // error[1] determines if a re-print is possible (in the case where the JSON is good, but the printer was not connected)
			            console.log('An error occurred: ' + error[0]);
                        navigator.notification.alert(
                            'An error occurred while printing the labels.' + error[0],  // message
                            alertDismissed,         // callback
                            'Error',            // title
                            'Ok'                  // buttonName
                        );
			        }}
                );
	        }}
try{{
            printLabels();
}} catch(e){{}}
            __doPostBack('btnCancel_Click','');
            ", jsonObject);
            ScriptManager.RegisterStartupScript(upContent, upContent.GetType(), "addLabelScript", script, true);
        }

        /// <summary>
        /// Formats the Zebra string.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="isJson">if set to <c>true</c> [is json].</param>
        /// <returns></returns>
        private static string ZebraFormatString(string input, bool isJson = false)
        {
            if (isJson)
            {
                return input.Replace("é", @"\\82");  // fix acute e
            }
            else
            {
                return input.Replace("é", @"\82");  // fix acute e
            }
        }
    }
    public class FamilyLabel
    {
        public Guid FileGuid { get; set; }

        public CheckInLabel LabelObj { get; set; }

        private List<string> _mergeFields = new List<string>();
        public List<string> MergeFields
        {
            get
            {
                return _mergeFields;
            }
            set
            {
                _mergeFields = value;
            }
        }
        private List<string> _mergeKeys = new List<string>();

        public List<string> MergeKeys
        {
            get
            {
                return _mergeKeys;
            }
            set
            {
                _mergeKeys = value;
            }
        }
    }
}