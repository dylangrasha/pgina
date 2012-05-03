﻿/*
	Copyright (c) 2011, pGina Team
	All rights reserved.

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions are met:
		* Redistributions of source code must retain the above copyright
		  notice, this list of conditions and the following disclaimer.
		* Redistributions in binary form must reproduce the above copyright
		  notice, this list of conditions and the following disclaimer in the
		  documentation and/or other materials provided with the distribution.
		* Neither the name of the pGina Team nor the names of its contributors 
		  may be used to endorse or promote products derived from this software without 
		  specific prior written permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
	ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
	DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY
	DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
	(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
	LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
	ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
	(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
	SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;

using pGina.Shared.Settings;
using pGina.Plugin.Ldap;

using log4net;

namespace pGina.Plugin.Ldap
{
    public partial class Configuration : Form
    {
        private ILog m_logger = LogManager.GetLogger("Ldap Configuration");

        public Configuration()
        {
            InitializeComponent();

            LoadSettings();
            UpdateSslElements();
            UpdateAuthenticationElements();

            this.gatewayRuleGroupMemberCB.SelectedIndex = 0;
        }

        private void LoadSettings()
        {
            string[] ldapHosts = Settings.Store.LdapHost;
            string hosts = "";
            for (int i = 0; i < ldapHosts.Count(); i++)
            {
                string host = ldapHosts[i];
                if (i < ldapHosts.Count() - 1) hosts += host + " ";
                else hosts += host;
            }
            ldapHostTextBox.Text = hosts;

            int port = Settings.Store.LdapPort;
            ldapPortTextBox.Text = Convert.ToString(port);

            int timeout = Settings.Store.LdapTimeout;
            timeoutTextBox.Text = Convert.ToString(timeout);

            bool useSsl = Settings.Store.UseSsl;
            useSslCheckBox.CheckState = useSsl ? CheckState.Checked : CheckState.Unchecked;

            bool reqCert = Settings.Store.RequireCert;
            validateServerCertCheckBox.CheckState = reqCert ? CheckState.Checked : CheckState.Unchecked;

            string serverCertFile = Settings.Store.ServerCertFile;
            sslCertFileTextBox.Text = serverCertFile;

            string searchDn = Settings.Store.SearchDN;
            searchDnTextBox.Text = searchDn;

            string searchPw = Settings.Store.GetEncryptedSetting("SearchPW");
            searchPassTextBox.Text = searchPw;

            string grpDnPattern = Settings.Store.GroupDnPattern;
            this.groupDNPattern.Text = grpDnPattern;

            string grpMemberAttrib = Settings.Store.GroupMemberAttrib;
            this.groupMemberAttrTB.Text = grpMemberAttrib;

            // Authentication tab
            bool allowEmpty = Settings.Store.AllowEmptyPasswords;
            this.allowEmptyPwCB.Checked = allowEmpty;

            string dnPattern = Settings.Store.DnPattern;
            dnPatternTextBox.Text = dnPattern;

            bool doSearch = Settings.Store.DoSearch;
            searchForDnCheckBox.CheckState = doSearch ? CheckState.Checked : CheckState.Unchecked;

            string filter = Settings.Store.SearchFilter;
            searchFilterTextBox.Text = filter;

            string[] searchContexts = Settings.Store.SearchContexts;
            string ctxs = "";
            for (int i = 0; i < searchContexts.Count(); i++)
            {
                string ctx = searchContexts[i];
                if (i < searchContexts.Count() - 1) ctxs += ctx + "\r\n";
                else ctxs += ctx;
            }
            searchContextsTextBox.Text = ctxs;

            /////////////// Authorization tab /////////////////
            this.authzRuleMemberComboBox.SelectedIndex = 0;
            this.authzRuleActionComboBox.SelectedIndex = 0;

            // Save rules
            List<GroupAuthzRule> lst = GroupRuleLoader.GetAuthzRules();
            // The last one should be the default rule
            if (lst.Count > 0 && 
                lst[lst.Count-1].RuleCondition == GroupRule.Condition.ALWAYS)
            {
                GroupAuthzRule rule = lst[lst.Count - 1];
                if (rule.AllowOnMatch)
                    this.authzDefaultAllowRB.Checked = true;
                else
                    this.authzDefaultDenyRB.Checked = true;
                lst.RemoveAt(lst.Count - 1);
            }
            else
            {
                // The list is empty or the last rule is not a default rule.
                throw new Exception("Default rule not found in rule list.");
            }
            // The rest of the rules
            foreach (GroupAuthzRule rule in lst)
                this.authzRulesListBox.Items.Add(rule);

            ///////////////// Gateway tab /////////////////
            List<GroupGatewayRule> gwLst = GroupRuleLoader.GetGatewayRules();
            foreach (GroupGatewayRule rule in gwLst)
                this.gatewayRulesListBox.Items.Add(rule);
        }

        private void sslCertFileBrowseButton_Click(object sender, EventArgs e)
        {
            DialogResult result;
            string fileName;
            using( OpenFileDialog dlg = new OpenFileDialog() )
            {
                result = dlg.ShowDialog();
                fileName = dlg.FileName;
            }

            if( result == DialogResult.OK )
            {
                sslCertFileTextBox.Text = fileName;
            }
        }

        private void useSslCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSslElements();
        }

        private void UpdateSslElements()
        {
            if (validateServerCertCheckBox.CheckState == CheckState.Checked)
            {
                sslCertFileTextBox.Enabled = true;
                sslCertFileBrowseButton.Enabled = true;
            }
            else if (validateServerCertCheckBox.CheckState == CheckState.Unchecked)
            {
                sslCertFileTextBox.Enabled = false;
                sslCertFileBrowseButton.Enabled = false;
            }

            if (useSslCheckBox.CheckState == CheckState.Unchecked)
            {
                validateServerCertCheckBox.Enabled = false;
                sslCertFileTextBox.Enabled = false;
                sslCertFileBrowseButton.Enabled = false;
            }
            else if (useSslCheckBox.CheckState == CheckState.Checked)
            {
                validateServerCertCheckBox.Enabled = true;
            }
        }

        private void UpdateAuthenticationElements()
        {
            if (searchForDnCheckBox.Checked)
            {
                searchFilterTextBox.Enabled = true;
                searchContextsTextBox.Enabled = true;
                dnPatternTextBox.Enabled = false;
            }
            else
            {
                searchFilterTextBox.Enabled = false;
                searchContextsTextBox.Enabled = false;
                dnPatternTextBox.Enabled = true;
            }
        }

        private void validateServerCertCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSslElements();
        }

        private void searchForDnCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAuthenticationElements();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (ValidateInput())
            {
                StoreSettings();
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
        }

        private bool ValidateInput()
        {
            if (ldapHostTextBox.Text.Trim().Length == 0)
            {
                MessageBox.Show("Please provide at least one LDAP host");
                return false;
            }

            try
            {
                int port = Convert.ToInt32(ldapPortTextBox.Text.Trim());
                if (port <= 0) throw new FormatException(); 
            }
            catch (FormatException)
            {
                MessageBox.Show("The LDAP port number must be a positive integer > 0.");
                return false;
            }

            try
            {
                int timeout = Convert.ToInt32(timeoutTextBox.Text.Trim());
                if (timeout <= 0) throw new FormatException();
            }
            catch (FormatException)
            {
                MessageBox.Show("The timout be a positive integer > 0.");
                return false;
            }

            if (validateServerCertCheckBox.CheckState == CheckState.Checked &&
                sslCertFileTextBox.Text.Trim().Length > 0 &&
                !(File.Exists(sslCertFileTextBox.Text.Trim())))
            {
                MessageBox.Show("SSL certificate file does not exist."
                    + "Please select a valid certificate file.");
                return false;
            }

            if (searchForDnCheckBox.Checked &&
                string.IsNullOrEmpty(searchFilterTextBox.Text.Trim()))
            {
                MessageBox.Show("Please provide a search filter when \"Search for DN\" is enabled.");
                return false;
            }

            if (searchForDnCheckBox.Checked &&
                string.IsNullOrEmpty(searchContextsTextBox.Text.Trim()))
            {
                MessageBox.Show("Please provide at least one search context when \"Search for DN\" is enabled.");
                return false;
            }

            if ( (searchForDnCheckBox.Checked ||
                authzRulesListBox.Items.Count > 0 ||
                gatewayRulesListBox.Items.Count > 0) && 
                string.IsNullOrEmpty(searchDnTextBox.Text.Trim()) )
            {
                MessageBox.Show("WARNING: You should provide a Search DN when \n" +
                    " \"Search for DN\" is enabled or using a group authorization \n" +
                    " or gateway rule.");
            }
            if ((authzRulesListBox.Items.Count > 0 ||
                gatewayRulesListBox.Items.Count > 0) && (
                string.IsNullOrEmpty(this.groupDNPattern.Text.Trim()) ||
                string.IsNullOrEmpty(this.groupMemberAttrTB.Text.Trim())) )
            {
                MessageBox.Show("WARNING: You should provide a Group DN Pattern and Member Attribute when \n" +
                    " using one or more group authorization or gateway rules.");
            }
            // TODO: Make sure that other input is valid.

            return true;
        }

        private void StoreSettings()
        {
            Settings.Store.LdapHost = Regex.Split(ldapHostTextBox.Text.Trim(), @"\s+"); 
            Settings.Store.LdapPort = Convert.ToInt32(ldapPortTextBox.Text.Trim());
            Settings.Store.LdapTimeout = Convert.ToInt32(timeoutTextBox.Text.Trim());
            Settings.Store.UseSsl = (useSslCheckBox.CheckState == CheckState.Checked);
            Settings.Store.RequireCert = (validateServerCertCheckBox.CheckState == CheckState.Checked);
            Settings.Store.ServerCertFile = sslCertFileTextBox.Text.Trim();
            Settings.Store.SearchDN = searchDnTextBox.Text.Trim();
            Settings.Store.SetEncryptedSetting("SearchPW", searchPassTextBox.Text);
            Settings.Store.GroupDnPattern = this.groupDNPattern.Text.Trim();
            Settings.Store.GroupMemberAttrib = this.groupMemberAttrTB.Text.Trim();
            
            // Authentication
            Settings.Store.AllowEmptyPasswords = this.allowEmptyPwCB.Checked;
            Settings.Store.DnPattern = dnPatternTextBox.Text.Trim();
            Settings.Store.DoSearch = (searchForDnCheckBox.CheckState == CheckState.Checked);
            Settings.Store.SearchFilter = searchFilterTextBox.Text.Trim();
            Settings.Store.SearchContexts = Regex.Split(searchContextsTextBox.Text.Trim(), @"\s*\r?\n\s*");
            
            // Authorization
            List<GroupAuthzRule> lst = new List<GroupAuthzRule>();
            foreach (Object item in this.authzRulesListBox.Items)
            {
                lst.Add(item as GroupAuthzRule);
                m_logger.DebugFormat("Saving rule: {0}", item);
            }
            // Add the default as the last rule in the list
            lst.Add(new GroupAuthzRule(this.authzDefaultAllowRB.Checked));

            GroupRuleLoader.SaveAuthzRules(lst);

            // Gateway
            List<GroupGatewayRule> gwList = new List<GroupGatewayRule>();
            foreach (Object item in this.gatewayRulesListBox.Items)
            {
                gwList.Add(item as GroupGatewayRule);
                m_logger.DebugFormat("Saving rule: {0}", item);
            }
            GroupRuleLoader.SaveGatewayRules(gwList);
        }

        private void showPwCB_CheckedChanged(object sender, EventArgs e)
        {
            this.searchPassTextBox.UseSystemPasswordChar = !this.showPwCB.Checked;
        }

        private void authzRuleAddButton_Click(object sender, EventArgs e)
        {
            string grp = this.authzRuleGroupTB.Text.Trim();
            if (string.IsNullOrEmpty(grp))
            {
                MessageBox.Show("Please enter a group name.");
                return;
            }

            int idx = this.authzRuleMemberComboBox.SelectedIndex;
            GroupRule.Condition c;
            if (idx == 0) c = GroupRule.Condition.MEMBER_OF;
            else if (idx == 1) c = GroupRule.Condition.NOT_MEMBER_OF;
            else
                throw new Exception("Unrecognized option in authzRuleAddButton_Click");
           

            idx = this.authzRuleActionComboBox.SelectedIndex;
            bool allow;
            if (idx == 0) allow = true;          // allow
            else if (idx == 1) allow = false;    // deny
            else
                throw new Exception("Unrecognized action option in authzRuleAddButton_Click");

            GroupAuthzRule rule = new GroupAuthzRule(grp, c, allow);
            this.authzRulesListBox.Items.Add(rule);
        }

        private void addGatewayGroupRuleButton_Click(object sender, EventArgs e)
        {
            string localGrp = this.gatewayLocalGroupTB.Text.Trim();
            if (string.IsNullOrEmpty(localGrp))
            {
                MessageBox.Show("Please enter a group name");
                return;
            }
            int idx = this.gatewayRuleGroupMemberCB.SelectedIndex;
            GroupRule.Condition c;
            if (idx == 0) c = GroupRule.Condition.MEMBER_OF;
            else if (idx == 1) c = GroupRule.Condition.NOT_MEMBER_OF;
            else if (idx == 2) c = GroupRule.Condition.ALWAYS;
            else
                throw new Exception("Unrecognized option in addGatewayGroupRuleButton_Click");

            if (c == GroupRule.Condition.ALWAYS)
            {
                this.gatewayRulesListBox.Items.Add(new GroupGatewayRule(localGrp));
            }
            else
            {
                string remoteGroup = this.gatwayRemoteGroupTB.Text.Trim();
                if (string.IsNullOrEmpty(remoteGroup))
                {
                    MessageBox.Show("Please enter a remote group name");
                    return;
                }
                this.gatewayRulesListBox.Items.Add(new GroupGatewayRule(remoteGroup, c, localGrp));
            }
        }

        private void gatewayRuleGroupMemberCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.gatewayRuleGroupMemberCB.SelectedIndex == 2)
            {
                this.gatwayRemoteGroupTB.Enabled = false;
                this.gatwayRemoteGroupTB.Text = "";
            }
            else
            {
                this.gatwayRemoteGroupTB.Enabled = true;
            }
        }

        private void gatewayRuleDeleteBtn_Click(object sender, EventArgs e)
        {
            int idx = this.gatewayRulesListBox.SelectedIndex;
            if( idx >= 0 && idx < this.gatewayRulesListBox.Items.Count )
                this.gatewayRulesListBox.Items.RemoveAt(idx); 
        }

        private void authzRuleDeleteBtn_Click(object sender, EventArgs e)
        {
            int idx = this.authzRulesListBox.SelectedIndex;
            if (idx >= 0 && idx < this.authzRulesListBox.Items.Count)
                this.authzRulesListBox.Items.RemoveAt(idx);
        }

        private void authzRuleUpBtn_Click(object sender, EventArgs e)
        {
            int idx = this.authzRulesListBox.SelectedIndex;
            if (idx > 0 && idx < this.authzRulesListBox.Items.Count)
            {
                object item = this.authzRulesListBox.Items[idx];
                this.authzRulesListBox.Items.RemoveAt(idx);
                this.authzRulesListBox.Items.Insert(idx-1,item);
                this.authzRulesListBox.SelectedIndex = idx - 1;
            }
        }

        private void authzRuleDownBtn_Click(object sender, EventArgs e)
        {
            int idx = this.authzRulesListBox.SelectedIndex;
            if (idx >= 0 && idx < this.authzRulesListBox.Items.Count - 1)
            {
                object item = this.authzRulesListBox.Items[idx];
                this.authzRulesListBox.Items.RemoveAt(idx);
                this.authzRulesListBox.Items.Insert(idx + 1, item);
                this.authzRulesListBox.SelectedIndex = idx + 1;
            }
        }


    }
}
