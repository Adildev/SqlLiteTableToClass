﻿using System;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace SqlLiteTableToClass
{
    public partial class Form1 : Form
    {
        private readonly string _directoryPath = Directory.GetParent(Application.ExecutablePath).FullName;

        private string _xmlPath;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            textBox1.Text = openFileDialog1.FileName;

            GetTables();
        }

        private void GetTables()
        {
            DataTable DataTable = GetDataTable("SELECT name FROM MAIN.[sqlite_master] WHERE type='table'");
            if (DataTable != null)
            {
                foreach (DataRow DataRow in DataTable.Rows)
                {
                    comboBox1.Items.Add(DataRow["name"].ToString());
                }
                comboBox1.SelectedIndex = 0;
            }
            else
                MessageBox.Show(@"Connection failed.");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!checkBox1.Checked)
            {
                DataTable DataTable = GetDataTable("select * from " + comboBox1.SelectedItem + " Limit 1");
                if (DataTable != null)
                {
                    GenerateXml(DataTable);

                    folderBrowserDialog.ShowDialog();

                    Cursor = Cursors.WaitCursor;
                    string ToolPath = Path.Combine(_directoryPath, "xsd.exe");
                    Process.Start(ToolPath, " " + _xmlPath + " /c /o:" + folderBrowserDialog.SelectedPath + " /n:");


                    RemoveExtrasFromClass();
                    Cursor = Cursors.Default;
                }
            }
            else
                GenenrateBAL();
        }

        private void GenenrateBAL()
        {
            string TempletePath = Path.Combine(_directoryPath, "BLLTemplete.ad");
            string ClassName = comboBox1.SelectedItem.ToString();
            string templete = File.ReadAllText(TempletePath);

            templete = templete.Replace("@@CLASSNAME", ClassName);

            DataTable DataTable = GetDataTable("select * from " + comboBox1.SelectedItem + " Limit 1");
            string BuildSearchProperties = "";
            string BuilInsertProperties = "";

            //---------------------------PROPERTIES-----------------------------------------------------
            const string searchTemplete = "obj@@CLASSNAME.@@PROPERTNAME = @@DATATYPE(dr[\"@@PROPERTNAME\"].ToString());";
            const string insertTemplete =
                " p[@@INDEX] = new SQLiteParameter(\"@@@PROPERTNAME\", obj@@CLASSNAME.@@PROPERTNAME);";
            for (int i = 0; i < DataTable.Columns.Count; i++)
            {
                DataColumn DataColumn = DataTable.Columns[i];

                BuildSearchProperties = BuildSearchProperties +
                                        searchTemplete.Replace("@@CLASSNAME", ClassName)
                                            .Replace("@@PROPERTNAME", DataColumn.ColumnName)
                                            .Replace("@@DATATYPE",
                                                (DataColumn.DataType.Name == "String"
                                                    ? ""
                                                    : (DataColumn.DataType.Name + ".Parse"))) + "\r\n\t\t\t\t";
                BuilInsertProperties = BuilInsertProperties +
                                       insertTemplete.Replace("@@CLASSNAME", ClassName)
                                           .Replace("@@PROPERTNAME", DataColumn.ColumnName)
                                           .Replace("@@INDEX", i.ToString()) + "\r\n\t\t\t\t";
            }
            templete = templete.Replace("@@FOREACHSEARCHPROPERTY", BuildSearchProperties);
            templete = templete.Replace("@@INSERTPARAMETERCOUNT", DataTable.Columns.Count.ToString());
            templete = templete.Replace("@@FOREACHINSERTPROPERTY", BuilInsertProperties);
            //---------------------------------------------------------------------------------------------------

            folderBrowserDialog.ShowDialog();
            File.WriteAllText(Path.Combine(folderBrowserDialog.SelectedPath, ClassName + "BLL.cs"), templete);
        }

        private void RemoveExtrasFromClass()
        {
            Thread.Sleep(2000);
            string GeneratedClassPath = Path.Combine(folderBrowserDialog.SelectedPath, comboBox1.SelectedItem + ".cs");

            string ReadAllText = File.ReadAllText(GeneratedClassPath);
            ReadAllText =
                ReadAllText.Replace(
                    "[System.Xml.Serialization.XmlElementAttribute(Form=System.Xml.Schema.XmlSchemaForm.Unqualified)]\r\n",
                    "");
            ReadAllText =
                ReadAllText.Replace("\r\n[System.CodeDom.Compiler.GeneratedCodeAttribute(\"xsd\", \"0.0.0.0\")]", "");
            ReadAllText = ReadAllText.Replace("\r\n[System.SerializableAttribute()]", "");
            ReadAllText = ReadAllText.Replace("\r\n[System.Diagnostics.DebuggerStepThroughAttribute()]", "");
            ReadAllText = ReadAllText.Replace("\r\n[System.ComponentModel.DesignerCategoryAttribute(\"code\")]", "");
            ReadAllText = ReadAllText.Replace("\r\n[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true)]", "");
            ReadAllText =
                ReadAllText.Replace(
                    "\r\n[System.Xml.Serialization.XmlRootAttribute(Namespace=\"\", IsNullable=false)]", "");
            ReadAllText = ReadAllText.Replace(@"/// <remarks/>", "");
            ReadAllText = ReadAllText.Replace("partial ", "");
            ReadAllText =
                ReadAllText.Replace(
                    "//------------------------------------------------------------------------------\r\n// <auto-generated>\r\n//     This code was generated by a tool.\r\n//     Runtime Version:4.0.30319.34014\r\n//\r\n//     Changes to this file may cause incorrect behavior and will be lost if\r\n//     the code is regenerated.\r\n// </auto-generated>\r\n//------------------------------------------------------------------------------\r\n\r\n// \n//This source code was auto-generated by MonoXSD\n//\r\n\r\n\r\n\r\n",
                    "");
            File.WriteAllText(GeneratedClassPath, ReadAllText);
        }

        public DataTable GetDataTable(string sql)
        {
            DataTable dt = new DataTable();

            try
            {
                SQLiteConnection cnn = new SQLiteConnection(@"Data Source=" + textBox1.Text);

                cnn.Open();

                SQLiteCommand mycommand = new SQLiteCommand(cnn) {CommandText = sql};

                SQLiteDataReader reader = mycommand.ExecuteReader();

                dt.Load(reader);

                reader.Close();

                cnn.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Error: " + ex.Message);
                return null;
            }

            return dt;
        }

        private void GenerateXml(DataTable dataTable)
        {
            _xmlPath = _directoryPath + "//" + comboBox1.SelectedItem + ".xsd";
            XmlSerializer xs = new XmlSerializer(typeof (DataTable));
            StringWriter sw = new StringWriter();
            xs.Serialize(sw, dataTable);

            XmlDocument xd = new XmlDocument();
            xd.LoadXml(sw.ToString());
            XmlNode SelectSingleNode = xd.SelectSingleNode("DataTable");
            if (SelectSingleNode != null)
            {
                string TableXml = SelectSingleNode.ChildNodes[0].ChildNodes[0].ChildNodes[0].ChildNodes[0].InnerXml;
                TableXml =
                    "<xs:schema id=\"NewDataSet\" xmlns=\"\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">" +
                    TableXml + "</xs:schema>";

                TableXml = TableXml.Replace("minOccurs=\"0\"", "minOccurs=\"1\"");
                File.WriteAllText(_xmlPath, TableXml);
            }
        }


        private void button3_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}