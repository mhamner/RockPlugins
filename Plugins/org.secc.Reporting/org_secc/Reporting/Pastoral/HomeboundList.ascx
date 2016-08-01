﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="HomeboundList.ascx.cs" Inherits="RockWeb.Blocks.Reporting.HomeboundList" %>

<asp:UpdatePanel ID="upReport" runat="server">
    <ContentTemplate>



            <div class="panel panel-block">
                <div class="panel-heading">
                    <h1 class="panel-title"><i class="fa fa-bed"></i> Homebound List</h1>
                </div>
                
                <Rock:Grid ID="gReport" runat="server" AllowSorting="true" EmptyDataText="No Results" DataKeyNames="Id" OnRowSelected="gReport_RowSelected">
                    <Columns>
                        <Rock:PersonField DataField="HomeboundPerson" HeaderText="Person" SortExpression="HomeboundPerson.LastName" />
                        <Rock:RockBoundField DataField="Age" HeaderText="Age" SortExpression="Age"></Rock:RockBoundField>
                        <Rock:RockBoundField DataField="StartDate" HeaderText="Start Date" SortExpression="StartDate"></Rock:RockBoundField>
                        <Rock:RockBoundField DataField="Description" ColumnPriority="DesktopLarge" HeaderText="Description" SortExpression="Description"></Rock:RockBoundField>
                        <Rock:RockBoundField DataField="Visits" HeaderText="Visits" SortExpression="visits"></Rock:RockBoundField>
                        <Rock:RockBoundField DataField="LastVisitor" HeaderText="Last Visitor" SortExpression="LastVisitor"></Rock:RockBoundField>
                        <Rock:RockBoundField DataField="LastVisitDate" HeaderText="Last Visit Date" SortExpression="LastVisitDate"></Rock:RockBoundField>
                        <Rock:RockBoundField DataField="LastVisitNotes" HeaderText="Last Visit Notes" SortExpression="LastVisitNotes"></Rock:RockBoundField>
                        <Rock:RockTemplateField HeaderText="Status" ColumnPriority="DesktopLarge">
                            <ItemTemplate>
                                <span class="label label-success"><%# Eval("Status") %></span>
                            </ItemTemplate>
                        </Rock:RockTemplateField>
                        <Rock:BoolField DataField="Communion" HeaderText="Com." />
                        <Rock:RockTemplateField HeaderText="Actions" ItemStyle-Width="120px">
                            <ItemTemplate>
                                <a href="<%# "https://maps.google.com/?q="+Eval("Address").ToString() %>" target="_blank" class="btn btn-default"><i class="fa fa-map-o" title="View Map"></i></a>
                                <a href="<%# "/Pastoral/Homebound/"+Eval("Workflow.Id") %>" class="btn btn-default"><i class="fa fa-pencil"></i></a>
                            </ItemTemplate>
                        </Rock:RockTemplateField>
                    </Columns>
                </Rock:Grid>
            </div>

    </ContentTemplate>
</asp:UpdatePanel>