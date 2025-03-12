using System;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Data.OleDb;

namespace RaynorJdeApi
{
    /// <summary>
    /// Summary description for Database.
    /// </summary>
    public class Database : IDisposable
    {
        private bool bSql = true;
        private string dError = "";
        private string jdeTableBase;
        private DataSet dDataSet = null;
        private DataRow dDataRow = null;
        private SqlConnection dSqlConnection = null;
        private SqlDataAdapter dSqlAdapter = null;
        private SqlDataReader dSqlReader = null;
        private OleDbConnection dOleDbConnection = null;
        private OleDbDataAdapter dOleDbAdapter = null;
        private OleDbDataReader dOleDbReader = null;
        private bool disposed = false;

        public Database(bool sql)
        {
            bSql = sql;
            if (bSql)
            {
                dSqlAdapter = new SqlDataAdapter();
            }
            else
            {
                dOleDbAdapter = new OleDbDataAdapter();
            }
            dDataSet = new System.Data.DataSet();
        }
        public Database(string sConnectString, string _jdeTableBase, bool sql)
        {
            bSql = sql;
            if (bSql)
            {
                dSqlAdapter = new SqlDataAdapter();
            }
            else
            {
                dOleDbAdapter = new OleDbDataAdapter();
            }
            dDataSet = new System.Data.DataSet();
            Connect(sConnectString);
            jdeTableBase = _jdeTableBase;
        }
        public string Error
        {
            get { return dError; }
            set { dError = value; }
        }
        public DataSet DSet
        {
            get { return dDataSet; }
        }
        public DataRow DRow
        {
            get { return dDataRow; }
            set { dDataRow = value; }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    dDataSet = null;
                    dDataRow = null;
                    dSqlReader = null;
                    dSqlAdapter.Dispose();
                    if (dSqlConnection != null)
                    {
                        dSqlConnection.Close();
                        dSqlConnection.Dispose();
                    }
                    dOleDbReader = null;
                    dOleDbAdapter.Dispose();
                    if (dOleDbConnection != null)
                    {
                        dOleDbConnection.Close();
                        dOleDbConnection.Dispose();
                    }
                }
            }
            disposed = true;
        }

        //
        // Connect to database
        //
        public bool Connect(string sConnectString)
        {
            bool bResult = false;
            dError = "";

            if (sConnectString != "")
            {
                if (bSql)
                {
                    if (dSqlConnection != null)
                    {
                        dSqlConnection.Dispose();
                        dSqlConnection = null;
                    }
                    dSqlConnection = new SqlConnection(sConnectString);
                    try
                    {
                        dSqlConnection.Open();
                        dSqlConnection.Close();
                        bResult = true;
                    }
                    catch (System.Exception e)
                    {
                        dError = "Error: " + e.Message;
                        dSqlConnection = null;
                    }
                }
                else
                {
                    if (dOleDbConnection != null)
                    {
                        dOleDbConnection.Dispose();
                        dOleDbConnection = null;
                    }
                    dOleDbConnection = new OleDbConnection(sConnectString);
                    try
                    {
                        dOleDbConnection.Open();
                        dOleDbConnection.Close();
                        bResult = true;
                    }
                    catch (System.Exception e)
                    {
                        dError = "Error: " + e.Message;
                        dOleDbConnection = null;
                    }
                }
            }
            else
                dError = "Missing connection string";

            return bResult;
        }

        //
        // Execute a SQL command and return a true or false
        //
        public bool ExecuteCommand(string sSQLCommand)
        {
            bool bResults = false;
            string sMessage = "";
            dError = "";

            if (sSQLCommand != "")
            {
                if (bSql)
                {
                    if (dSqlConnection != null)
                    {
                        try
                        {
                            dSqlConnection.Open();
                            SqlCommand dSqlCommand = new SqlCommand(sSQLCommand);
                            dSqlCommand.Connection = dSqlConnection;
                            bResults = dSqlCommand.ExecuteNonQuery() > 0 ? true : false;
                            dSqlCommand.Dispose();
                        }
                        catch (SqlException e)
                        {
                            dError = "Error: " + e.Message;
                            if (e.Class < 17)
                                sMessage = "<E>" + e.Message + " - " + sSQLCommand;
                        }
                        finally
                        {
                            dSqlConnection.Close();
                        }
                    }
                }
                else
                {
                    if (dOleDbConnection != null)
                    {
                        try
                        {
                            sSQLCommand = sSQLCommand.Replace("CRPDTA", jdeTableBase);
                            dOleDbConnection.Open();
                            OleDbCommand dOleDbCommand = new OleDbCommand(sSQLCommand);
                            dOleDbCommand.Connection = dOleDbConnection;
                            bResults = dOleDbCommand.ExecuteNonQuery() > 0 ? true : false;
                            dOleDbCommand.Dispose();
                        }
                        catch (OleDbException e)
                        {
                            dError = "Error: " + e.Message;
                        }
                        finally
                        {
                            dOleDbConnection.Close();
                        }
                    }
                }
            }

            return bResults;
        }

        //
        // Execute a SQL command and return the identity field
        //
        public string ExecuteScalar(string sSQLCommand)
        {
            string sResults = "";
            string sMessage = "";
            dError = "";

            if (sSQLCommand != "")
            {
                if (bSql)
                {
                    if (dSqlConnection != null)
                        try
                        {
                            dSqlConnection.Open();
                            SqlCommand dSqlCommand = new SqlCommand(sSQLCommand + ";SELECT SCOPE_IDENTITY();");
                            dSqlCommand.Connection = dSqlConnection;
                            sResults = dSqlCommand.ExecuteScalar().ToString();
                            dSqlCommand.Dispose();
                        }
                        catch (SqlException e)
                        {
                            dError = "Error: " + e.Message;
                            if (e.Class < 17)
                                sMessage = "<E>" + e.Message + " - " + sSQLCommand;
                        }
                        finally
                        {
                            dSqlConnection.Close();
                        }
                }
                else
                {
                    if (dOleDbConnection != null)
                    {
                        try
                        {
                            sSQLCommand = sSQLCommand.Replace("CRPDTA", jdeTableBase);
                            dOleDbConnection.Open();
                            OleDbCommand dOleDbCommand = new OleDbCommand(sSQLCommand + ";SELECT SCOPE_IDENTITY();");
                            dOleDbCommand.Connection = dOleDbConnection;
                            sResults = dOleDbCommand.ExecuteScalar().ToString();
                            dOleDbCommand.Dispose();
                        }
                        catch (OleDbException e)
                        {
                            dError = "Error: " + e.Message;
                        }
                        finally
                        {
                            dOleDbConnection.Close();
                        }
                    }
                }
            }

            return sResults;
        }

        //
        // Execute a SQL command to return a single field
        //
        public string GetField(string sSQLCommand)
        {
            string sResults = "";
            string sMessage = "";
            dError = "";

            if (sSQLCommand != "")
            {
                if (bSql)
                {
                    if (dSqlConnection != null)
                    {
                        try
                        {
                            dSqlConnection.Open();
                            SqlCommand dSqlCommand = new SqlCommand(sSQLCommand);
                            dSqlCommand.Connection = dSqlConnection;
                            dSqlReader = null;
                            dSqlReader = dSqlCommand.ExecuteReader();

                            dSqlReader.Read();
                            if (dSqlReader.HasRows)
                                sResults = dSqlReader.GetValue(0).ToString();
                        }
                        catch (SqlException e)
                        {
                            dError = "Error: " + e.Message;
                            if (e.Class < 17)
                                sMessage = "<E>" + e.Message + " - " + sSQLCommand;
                        }
                        finally
                        {
                            if (dSqlReader != null)
                                dSqlReader.Close();
                            dSqlConnection.Close();
                        }
                    }
                }
                else
                {
                    if (dOleDbConnection != null)
                    {
                        try
                        {
                            sSQLCommand = sSQLCommand.Replace("CRPDTA", jdeTableBase);
                            dOleDbConnection.Open();
                            OleDbCommand dOleDbCommand = new OleDbCommand(sSQLCommand);
                            dOleDbCommand.Connection = dOleDbConnection;
                            dOleDbReader = null;
                            dOleDbReader = dOleDbCommand.ExecuteReader();

                            dOleDbReader.Read();
                            if (dOleDbReader.HasRows)
                                sResults = dOleDbReader.GetValue(0).ToString();
                        }
                        catch (OleDbException e)
                        {
                            dError = "Error: " + e.Message;
                        }
                        finally
                        {
                            if (dOleDbReader != null)
                                dOleDbReader.Close();
                            dOleDbConnection.Close();
                        }
                    }
                }
            }

            return sResults;
        }

        //
        // Execute a SQL command to return a single field
        //
        public string GetField2(string sSQLCommand, string sSQLCommand2)
        {
            string sResults = "";
            string sMessage = "";
            dError = "";

            if (sSQLCommand != "")
            {
                if (bSql)
                {
                    if (dSqlConnection != null)
                    {
                        try
                        {
                            dSqlConnection.Open();
                            SqlCommand dSqlCommand = new SqlCommand(sSQLCommand);
                            dSqlCommand.Connection = dSqlConnection;
                            dSqlReader = null;
                            dSqlReader = dSqlCommand.ExecuteReader();

                            dSqlReader.Read();
                            if (dSqlReader.HasRows)
                            {
                                sResults = dSqlReader.GetValue(0).ToString();
                                if (dSqlReader != null)
                                {
                                    dSqlReader.Close();
                                    dSqlReader = null;
                                }
                                dSqlCommand = new SqlCommand(sSQLCommand2.Replace("%1%", sResults));
                                dSqlCommand.Connection = dSqlConnection;
                                dSqlCommand.ExecuteNonQuery();
                                dSqlCommand.Dispose();
                            }
                        }
                        catch (SqlException e)
                        {
                            dError = "Error: " + e.Message;
                            if (e.Class < 17)
                                sMessage = "<E>" + e.Message + " - " + sSQLCommand;
                        }
                        finally
                        {
                            if (dSqlReader != null)
                                dSqlReader.Close();
                            dSqlConnection.Close();
                        }
                    }
                }
                else
                {
                    if (dOleDbConnection != null)
                    {
                        try
                        {
                            sSQLCommand = sSQLCommand.Replace("CRPDTA", jdeTableBase);
                            dOleDbConnection.Open();
                            OleDbCommand dOleDbCommand = new OleDbCommand(sSQLCommand);
                            dOleDbCommand.Connection = dOleDbConnection;
                            dOleDbReader = null;
                            dOleDbReader = dOleDbCommand.ExecuteReader();

                            dOleDbReader.Read();
                            if (dOleDbReader.HasRows)
                                sResults = dOleDbReader.GetValue(0).ToString();
                        }
                        catch (OleDbException e)
                        {
                            dError = "Error: " + e.Message;
                        }
                        finally
                        {
                            if (dOleDbReader != null)
                                dOleDbReader.Close();
                            dOleDbConnection.Close();
                        }
                    }
                }
            }

            return sResults;
        }

        //
        // Execute a SQL command to return a table in a dataset
        //
        public int GetTable(string sSQLCommand, string sTableName)
        {
            int iResult = 0;
            string sMessage = "";
            dError = "";

            if (dDataSet.Tables.Contains(sTableName) == true)
                dDataSet.Tables.Remove(sTableName);

            if (sSQLCommand != "")
            {
                if (bSql)
                {
                    if (dSqlConnection != null)
                    {
                        try
                        {
                            dSqlConnection.Open();
                            SqlCommand dSqlCommand = new SqlCommand(sSQLCommand);
                            dSqlCommand.Connection = dSqlConnection;
                            dSqlAdapter.SelectCommand = dSqlCommand;

                            dSqlAdapter.Fill(dDataSet, sTableName);
                            dSqlCommand.Dispose();

                            if (dDataSet.Tables.Contains(sTableName) == true)
                                iResult = dDataSet.Tables[sTableName].Rows.Count;
                        }
                        catch (SqlException e)
                        {
                            dError = "Error: " + e.Message;
                            if (e.Class < 17)
                                sMessage = "<E>" + e.Message + " - " + sSQLCommand;
                        }
                        finally
                        {
                            dSqlConnection.Close();
                        }
                    }
                }
                else
                {
                    if (dOleDbConnection != null)
                    {
                        try
                        {
                            sSQLCommand = sSQLCommand.Replace("CRPDTA", jdeTableBase);
                            dOleDbConnection.Open();
                            OleDbCommand dOleDbCommand = new OleDbCommand(sSQLCommand);
                            dOleDbCommand.Connection = dOleDbConnection;
                            dOleDbAdapter.SelectCommand = dOleDbCommand;

                            dOleDbAdapter.Fill(dDataSet, sTableName);
                            dOleDbCommand.Dispose();

                            if (dDataSet.Tables.Contains(sTableName) == true)
                                iResult = dDataSet.Tables[sTableName].Rows.Count;
                        }
                        catch (OleDbException e)
                        {
                            dError = "Error: " + e.Message;
                        }
                        finally
                        {
                            dOleDbConnection.Close();
                        }
                    }
                }
            }

            return iResult;
        }

        //
        // Perform a table update using a SQL statement
        //
        public bool Update(string sSQLCommand, string sTableName)
        {
            bool bResult = false;
            dError = "";

            if (sSQLCommand != "" && dSqlConnection != null)
            {
                try
                {
                    dSqlConnection.Open();
                    SqlCommand dSqlCommand = new SqlCommand(sSQLCommand);
                    dSqlCommand.Connection = dSqlConnection;
                    dSqlAdapter.SelectCommand = dSqlCommand;
                    SqlCommandBuilder dSqlCommandBuilder = new SqlCommandBuilder(dSqlAdapter);
                    dSqlAdapter.Update(dDataSet, sTableName);
                    dSqlCommand.Dispose();
                    dSqlCommandBuilder.Dispose();
                    bResult = true;
                }
                catch (System.Exception e)
                {
                    dError = "Error: " + e.Message;
                }
                finally
                {
                    dSqlConnection.Close();
                }
            }

            return bResult;
        }

        //
        // Check the existence of a table
        //
        public bool TableExists(string sTableName)
        {
            bool bResult = false;

            string sSQLCommand = "SELECT table_name AS Name FROM INFORMATION_SCHEMA.Tables WHERE TABLE_TYPE ='BASE TABLE'";
            int iCount = GetTable(sSQLCommand, "Table_Names");
            for (int i = 0; i < iCount; i++)
            {
                if (Convert.ToString(dDataSet.Tables["Table_Names"].Rows[i].ItemArray.GetValue(0)) == sTableName)
                {
                    bResult = true;
                    break;
                }
            }

            return bResult;
        }
    }
}
