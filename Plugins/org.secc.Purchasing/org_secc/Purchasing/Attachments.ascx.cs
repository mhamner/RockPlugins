﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using org.secc.Purchasing;
using Rock.Web.UI;


namespace RockWeb.Plugins.org_secc.Purchasing
{
    public partial class Attachments : UserControl
    {

       
        #region Properties
        public string ObjectTypeName
        {
            get
            {
                string otn = String.Empty;
                if (ViewState["Attachment_ObjectTypeName"] != null)
                    otn = ViewState["Attachment_ObjectTypeName"].ToString();

                return otn;
            }
            set
            {
                ViewState["Attachment_ObjectTypeName"] = value;
            }
        }

        public int Identifier
        {
            get
            {
                int id = 0;
                if (ViewState["Attachment_Identifier"] != null)
                    int.TryParse(ViewState["Attachment_Identifier"].ToString(), out id);
                return id;
            }
            set
            {
                ViewState["Attachment_Identifier"] = value;
            }
        }

        public bool ReadOnly
        {
            get
            {
                bool readOnly = false;
                if (ViewState["Attachment_ReadOnly"] != null)
                    bool.TryParse(ViewState["Attachment_ReadOnly"].ToString(), out readOnly);
                return readOnly;
            }
            set
            {
                ViewState["Attachment_ReadOnly"] = value;
            }
        }

        public string DocumentBrowserPage
        {
            get
            {
                string browserPage = String.Empty;
                if (ViewState["Attachment_DocumentBrowserPage"] != null)
                    browserPage = ViewState["Attachment_DocumentBrowserPage"].ToString();
                return browserPage;
            }
            set
            {
                ViewState["Attachment_DocumentBrowserPage"] = value;
            }
        }

        private Rock.Model.UserLogin mCurrentUser = null;
        public Rock.Model.UserLogin CurrentUser
        {

            get
            {
                return mCurrentUser;
            }
            set
            {
                mCurrentUser = value;
            }
        }
        #endregion

        #region Page Events
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            if (SaveAttachment())
            {
                ClearFields();
                LoadAttachmentList();
                RefreshParent(sender, e);
            }
        }

        protected void dgAttachment_ItemDataBind(object sender, DataGridItemEventArgs e)
        {
            if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
            {
                DataRowView DRV = (DataRowView)e.Item.DataItem;
                LinkButton lbEdit = (LinkButton)e.Item.FindControl("lbEdit");
                LinkButton lbHide = (LinkButton)e.Item.FindControl("lbHide");

                lbHide.CommandArgument = DRV["AttachmentID"].ToString();

                lbEdit.OnClientClick = "openChooseDocumentWindow(\"" + DRV["BlobID"].ToString()  +" \",\"" + Attachment.GetPurchasingDocumentType().TypeId + "\");return false;";

                bool IsEditable = UserCanEditItem(DRV["CreatedByUser"].ToString());
                lbEdit.Visible = IsEditable;
                lbHide.Visible = IsEditable;

            }
        }

        protected void dgAttachment_ItemCommand(object sender, DataGridCommandEventArgs e)
        {
            bool ReloadList = false;
            if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
            {
                int AttachmentID = 0;
                if (!int.TryParse(e.CommandArgument.ToString(), out AttachmentID))
                {
                    throw new RequisitionException("Attachment ID not found");
                }

                switch (e.CommandName.ToLower())
                {
                    case "hide":
                        HideAttachment(AttachmentID);
                        ReloadList = true;
                        RefreshParent(sender, e);
                        break;
                }
            }

            if (ReloadList)
                LoadAttachmentList();
        }

        #endregion

        public void LoadAttachmentControl(string otn, int identifier)
        {
            if (String.IsNullOrEmpty(otn))
                throw new ArgumentNullException("Object Type Name", "Object Type Name is required.");
            if (identifier <= -1)
                throw new ArgumentOutOfRangeException("Identifier", "Identifer must be 0 or greater.");

            ObjectTypeName = otn;
            Identifier = identifier;

            LoadAttachmentList();
            ClearFields();

        }

        public event EventHandler RefreshParent;

        #region Private

        private void ClearFields()
        {
            ihBlobID.Value = String.Empty;
            ihDisplayAtTopExpire.Value = String.Empty;
        }

        private void HideAttachment(int aID)
        {
            Attachment a = new Attachment(aID);
            a.Active = false;
            a.Save(CurrentUser.UserName);
        }

        private void LoadAttachmentList()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[]
                {
                    new DataColumn("AttachmentID"),
                    new DataColumn("Title"),
                    new DataColumn("Description"),
                    new DataColumn("FileType"),
                    new DataColumn("CreatedBy"),
                    new DataColumn("CreatedByUser"),
                    new DataColumn("DateModified"),
                    new DataColumn("BlobID"),
                    new DataColumn("BlobGuid")
                });

            //Where display at top has not expired, place objects  in reverse order of expiration date and by AttachmentID 
            //If it has expired display in order by AttachmentID
            List<Attachment> AttachmentList = Attachment.GetObjectAttachments(ObjectTypeName, Identifier, true)
                                .OrderBy(a => a.AttachmentID).ToList();

            foreach (var a in AttachmentList)
            {
                DataRow dr = dt.NewRow();
                dr["AttachmentID"] = a.AttachmentID;
                dr["CreatedBy"] = a.CreatedBy != null || a.CreatedBy.PrimaryAliasId <= 0 ? a.CreatedBy.FullName : a.CreatedByUserID;
                dr["CreatedByUser"] = a.CreatedByUserID;
                dr["DateModified"] = a.DateModified;

                if (a.DataBlob != null)
                {
                    dr["BlobID"] = a.DataBlob.Id;
                    dr["BlobGuid"] = a.DataBlob.Guid;
                    
                    dr["Title"] = a.DataBlob.FileName;

                    dr["Description"] = a.DataBlob.Description;
                    dr["FileType"] = a.DataBlob.MimeType;
                }

                dt.Rows.Add(dr);
            }

            dgAttachment.DataSource = dt;
            dgAttachment.DataBind();
        }

        private bool SaveAttachment()
        {
            int blobID = 0;
            DateTime datExpire = DateTime.MinValue;
            if (!int.TryParse(ihBlobID.Value, out blobID))
                return false;

            DateTime.TryParse(ihDisplayAtTopExpire.Value, out datExpire);

            Attachment a = Attachment.LoadByBlobID(ObjectTypeName, Identifier, blobID);
            if (a == null)
            {
                a = new Attachment();
                a.ParentObjectTypeName = ObjectTypeName;
                a.ParentIdentifier = Identifier;
                a.BlobID = blobID;
            }

            a.Active = true;

            a.Save(CurrentUser.UserName);
            return true;
        }

        private bool UserCanEditItem(string UserID)
        {
            if (ReadOnly)
                return !ReadOnly;

            return (UserID == CurrentUser.UserName);

        }

        #endregion
    }
}