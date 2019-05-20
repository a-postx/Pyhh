using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Pyhh.ExpertSearcher
{
    public class ExpertReport
    {
        public ExpertReport(string name, string exportDir = null, List<DataTable> data = null)
        {
            Name = name;
            ExportDirectory = string.IsNullOrEmpty(exportDir) ? Environment.CurrentDirectory : exportDir;
            ExcelFilePath = ExportDirectory + "\\" + Name + ".xlsx";

            if (data != null && data.Count > 0)
                Data = data;

            Init();
        }

        public string Name { get; }
        public List<DataTable> Data { get; set; } = new List<DataTable>();
        public string ExportDirectory { get; }

        private ExcelFile ReportExcelFile { get; set; }
        private string ExcelFilePath { get; }

        private void Init()
        {

        }

        public void MakeDataTable<T>(List<T> items)
        {
            try
            {
                string tableName = typeof(T).GetFriendlyName().RemoveExcelSheetInvalidChars();
                DataTable result = new DataTable(tableName);

                foreach (PropertyInfo p in typeof(T).GetProperties())
                {
                    result.Columns.Add(p.Name, p.PropertyType == typeof(Int32) ? typeof(int) : typeof(string));
                }

                foreach (T item in items)
                {
                    PropertyInfo[] props = item.GetType().GetProperties();
                    DataRow dataRow = result.NewRow();
                    int i = 0;

                    foreach (PropertyInfo prop in props)
                    {
                        if (prop.PropertyType == typeof(List<string>))
                        {
                            object propertyValue = prop.GetValue(item, null);
                            IEnumerable enumerable = (IEnumerable) propertyValue;
                            string stringValue = string.Empty;
                            if (enumerable != null)
                            {
                                foreach (object element in enumerable)
                                {
                                    stringValue = stringValue == string.Empty ? element.ToString() : stringValue + "," + element;
                                }
                            }

                            dataRow[i] = RemoveReportSeaparatorChar(stringValue);
                        }
                        else
                        {
                            object propertyValue = prop.GetValue(item, null);
                            string columnValue = propertyValue?.ToString() ?? string.Empty;
                            dataRow[i] = RemoveReportSeaparatorChar(columnValue);
                        }

                        i++;
                    }
                    result.Rows.Add(dataRow);
                }

                result.TableName = typeof(T).Name.RemoveExcelSheetInvalidChars();

                Data.Add(result);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error creating report data table: " + e);
            }
        }

        public void ExportToCSV()
        {
            try
            {
                if (!Directory.Exists(ExportDirectory))
                {
                    Directory.CreateDirectory(ExportDirectory);
                }

                Data.ForEach(dt =>
                {
                    string csvPath = ExportDirectory + "\\" + dt.TableName + ".csv";

                    if (File.Exists(csvPath))
                    {
                        File.Delete(csvPath);
                    }

                    StringBuilder sb = new StringBuilder();
                    IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
                    sb.AppendLine(string.Join("`", columnNames));

                    foreach (DataRow row in dt.Rows)
                    {
                        IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
                        sb.AppendLine(string.Join("`", fields));
                    }

                    File.AppendAllText(csvPath, sb.ToString());
                });
            }
            catch (Exception e)
            {
                Console.WriteLine("Error exporting to csv: " + e);
            }
        }

        private string RemoveReportSeaparatorChar(string s) => String.Join(string.Empty, s.Split("`".ToCharArray()));

        private ExcelFile CreateExcelFile(string name)
        {
            ExcelFile file = new ExcelFile(name, ExportDirectory, true)
            {
                Author = "Test author", Company = "Test company", Subject = "ExpertSearcher report"
            };

            return file;
        }

        public void ExportToExcel()
        {
            try
            {
                ReportExcelFile = CreateExcelFile(Name);

                Data.ForEach(dt =>
                {
                    ReportExcelFile.AddWorkSheet(dt.TableName, dt);
                });

                ReportExcelFile.Save();
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot export report " + Name + " to excel: " + e);
            }
        }
    }
}
