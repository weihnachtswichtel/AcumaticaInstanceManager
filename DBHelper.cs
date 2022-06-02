using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcumaticaInstanceManager
{
    internal class DBHelper
    {
        string connectionString { get; }
        string dbName { get; }


        //Class to interact with DB Server (MS SQL only). Please be carefull with parameters specified.
        public DBHelper(string DBServer, string DBName)
        {
            connectionString = $"server={DBServer};Trusted_Connection=yes";
            dbName = DBName;
        }

        public void DropDB()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                using (var checkCommand = new SqlCommand($"SELECT db_id('{dbName}')", connection))
                {
                    connection.Open();
                    if (checkCommand.ExecuteScalar() != DBNull.Value)
                    {
                        string dropCommandText = @"ALTER DATABASE [" + dbName + @"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [" + dbName + "]";
                        SqlCommand dropCommand = new SqlCommand(dropCommandText, connection);
                        dropCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        public void setPasswordChangeRequired(bool changeRequired)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                string updateCommandText = string.Format(@"USE {0}; UPDATE Users set PasswordChangeOnNextLogin = '{1}' where Username = 'admin'", dbName, changeRequired);
                connection.Open();
                SqlCommand updateCommand = new SqlCommand(updateCommandText, connection);
                updateCommand.ExecuteNonQuery();
            }
        }
    }
}
