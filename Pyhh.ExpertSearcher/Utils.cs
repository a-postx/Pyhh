using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Pyhh.ExpertSearcher
{
    public static class Utils
    {
        public static DataTable ToDataTable<T>(this IEnumerable<T> items, string tableName = null)
        {
            tableName = tableName ?? typeof(T).GetFriendlyName().RemoveExcelSheetInvalidChars();
            DataTable table = new DataTable(tableName);

            try
            {
                PropertyInfo[] props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo prop in props)
                {
                    Type propType = prop.PropertyType;

                    if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        propType = new NullableConverter(propType).UnderlyingType;

                    table.Columns.Add(prop.Name, propType);
                }

                List<int> enumsIndexes = new List<int>();

                foreach (T item in items)
                {
                    var values = new object[props.Length];                    

                    for (var i = 0; i < props.Length; i++)
                    {
                        values[i] = props[i].GetValue(item, null);

                        if (props[i].PropertyType.BaseType.Name == "Enum")
                        {
                            enumsIndexes.Add(i);
                        }
                    }

                    table.Rows.Add(values);
                }            
            }
            catch (Exception e)
            {
                Console.WriteLine("Error converting enumerable of type " + typeof(T).Name + " to DataTable: " + e);
            }

            return table;
        }

        public static string GetFriendlyName(this Type type)
        {
            string friendlyName = type.Name;
            if (type.IsGenericType)
            {
                int iBacktick = friendlyName.IndexOf('`');
                if (iBacktick > 0)
                {
                    friendlyName = friendlyName.Remove(iBacktick);
                }
                friendlyName += "<";
                Type[] typeParameters = type.GetGenericArguments();
                for (int i = 0; i < typeParameters.Length; ++i)
                {
                    string typeParamName = typeParameters[i].Name;
                    friendlyName += (i == 0 ? typeParamName : "," + typeParamName);
                }
                friendlyName += ">";
            }

            return friendlyName;
        }

        public static string RemoveExcelSheetInvalidChars(this string input)
        {
            Regex regex = new Regex(@"[\s:?*`<>_\[\]/\\]+");
            return regex.Replace(input, "");
        }
    }
}
