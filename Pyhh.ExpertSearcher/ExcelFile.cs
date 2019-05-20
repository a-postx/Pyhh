using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Table;

namespace Pyhh.ExpertSearcher
{
    public class ExcelFile
    {
        public ExcelFile(string fileName, string fileDir, bool overwrite = false)
        {
            FileName = fileName;
            FileDirectory = fileDir;
            Overwrite = overwrite;
            AddPackage();
        }

        public string Author { get; set; }
        public string Subject { get; set; }
        public string Company { get; set; }
        public string FileDirectory { get; }
        public string FileName { get; }

        private bool Overwrite { get; }
        private ExcelPackage Package { get; set; }
        private List<ExcelWorksheet> WorkSheets { get; set; } = new List<ExcelWorksheet>();

        private void AddPackage()
        {
            try
            {
                string nameWithPath = $"{FileDirectory}\\{FileName}.xlsx";

                FileInfo newFile = new FileInfo(nameWithPath);

                if (newFile.Exists)
                {
                    if (Overwrite)
                    {
                        newFile.Delete();
                    }
                    else
                    {
                        throw new Exception("File " + nameWithPath + " is already exist.");
                    }
                }

                ExcelPackage package = new ExcelPackage(newFile);
                Package = package;
            }
            catch (Exception e)
            {
                throw new Exception("Error adding excel package for report " + FileName + ": ", e);
            }
        }

        public void AddWorkSheet(string name, DataTable table)
        {
            ExcelWorksheet worksheet = Package.Workbook.Worksheets.Add(name);
            WorkSheets.Add(worksheet);

            worksheet.DefaultRowHeight = 15;
            worksheet.DefaultColWidth = 12;

            worksheet.Cells["A1"].LoadFromDataTable(table, true, TableStyles.None);
        }

        private ExcelWorksheet GetWorksheet(string name)
        {
            ExcelWorksheet result = null;

            try
            {
                ExcelWorksheet sheet = WorkSheets.FirstOrDefault(w => w.Name == name);

                if (sheet != null)
                {
                    result = sheet;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error getting worksheet " + name + " for report " + FileName + ": ", e);
            }

            return result;
        }

        private int GetColumnIndex(ExcelWorksheet worksheet, string name)
        {
            int result = -1;

            try
            {
                int idx = worksheet
                    .Cells["1:1"]
                    .First(c => c.Value.ToString() == name)
                    .Start
                    .Column;

                result = idx;
            }
            catch (Exception e)
            {
                throw new Exception("Error getting column index for worksheet " + name + " and report " + FileName + ": ", e);
            }

            return result;
        }

        private void FormatColumn(ExcelWorksheet worksheet, int columnIndex)
        {
            worksheet.Column(columnIndex).Style.Numberformat.Format = "#";
        }

        public void Save()
        {
            try
            {
                Package.Save();
            }
            catch (Exception e)
            {
                throw new Exception("Error saving excel file for report " + FileName + ": ", e);
            }
        }
    }
}
