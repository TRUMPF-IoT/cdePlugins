// SPDX-FileCopyrightText: 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using nsCDEngine.BaseClasses;
using System.Threading;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines;
using nsCDEngine.Security;
using nsCDEngine.Engines.ThingService;

//ERROR Range 480-499

namespace CDMyMSSQLStorage
{
    /// <summary>
	/// Contains commonly used UIDs in Storage for ease of access
	/// </summary>
    internal class CommonTypeUIDs
    {
        public static string cdePUniqueID = TheStorageUtilities.GenerateUniqueIDFromType(typeof(cdeP), null);
        public static string thingUniqueID = TheStorageUtilities.GenerateUniqueIDFromType(typeof(TheThing), null);
        public static string thingStoreUniqueID= TheStorageUtilities.GenerateUniqueIDFromType(typeof(TheThingStore), null);
    }
	/// <summary>
	/// Summary description for URCbase.
	/// </summary>
	internal class TheSQLHelper 
	{
        public TheSQLHelper(string pSQLUID, string sqlpwd, string pSQLAddress, string pDBName,int RetrySeconds,int Retries)
        {
            MyBlobCache = new TheMirrorCache<TheBlobData>(TheBaseAssets.MyServiceHostInfo.TO.StorageCleanCycle);
            if (string.IsNullOrEmpty(pSQLAddress)) return;
            if (pSQLAddress.StartsWith("AZURE:"))
            {
                pSQLAddress = pSQLAddress.Substring(pSQLAddress.IndexOf(':') + 1);
                MyConnectionString = "Connection Reset=true;Max Pool Size=10000;Connection Lifetime=10;Enlist=false;Application Name=C-DEngine;Trusted_Connection=False;Encrypt=True;Password=" + sqlpwd + ";User ID=" + pSQLUID + ";Server=" + pSQLAddress + ";Database=" + pDBName;
            }
            else
                MyConnectionString = "Connection Reset=false;Max Pool Size=10000;Connection Lifetime=4;Enlist=false;Application Name=C-DEngine;Password=" + sqlpwd + ";User ID=" + pSQLUID + ";Server=" + pSQLAddress + ";Database=" + pDBName;   //Data Source=" + pSQLAddress + ";Initial Catalog=" + pDBName;
            mRetrySeconds = TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout;
            if (RetrySeconds>0)
                mRetrySeconds = RetrySeconds;
            mRetries = Retries;
        }

#region Helpers 
        protected void AppendError(string MainError,string LongError)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(480, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SQLHelpers", "SQL Error: " + MainError, eMsgLevel.l1_Error, LongError));
            TheCDEKPIs.EngineErrors++;
            TheCDEKPIs.TotalEngineErrors++;
        }

		/// <summary>
		/// Tuned
		/// </summary>
		/// <param name="oStr"></param>
		/// <returns></returns>
		public static string SQLEncode(object oStr)
		{
			if (oStr==null) return "";
            string iStr = TheCommonUtils.CStr(oStr);
			if (iStr.Length==0) return "";
			int pos=iStr.IndexOf("'",0, StringComparison.Ordinal); 
			if (pos>=0) 
			{
				if (iStr.IndexOf("--",pos, StringComparison.Ordinal)>=0)
					iStr=iStr.Substring(0,pos);
			}
			//</New>
			return iStr.Replace("'","''"); //+ CHAR(39) +'");
		}

        public int GetTypeIDFromSQLType(string sqlType, string length)
        {
            int typeID = 0;
            switch(sqlType)
            {
                case "nvarchar":
                    if (length.Equals("4000"))
                        typeID = 0;
                    else if (length.Equals("512"))
                        typeID = 1;
                    else if (length.Equals("1"))
                        typeID = 10;
                    else if(length.Equals("max"))
                        typeID = 11;
                    break;
                case "int":
                    typeID = 2;
                    break;
                case "float":
                    typeID = 3;
                    break;
                case "datetime":
                case "datetimeoffset":
                    typeID = 4;
                    break;
                case "bit":
                    typeID = 5;
                    break;
                case "uniqueidentifier":
                    typeID = 6;
                    break;
                case "bigint":
                    typeID = 8;
                    break;
                case "tinyint":
                    typeID = 9;
                    break;
                case "varbinary":
                    typeID = 12;
                    break;
                case "smallint":
                    typeID = 14;
                    break;
                default:
                    typeID = 1;
                    break;
            }
            return typeID;
        }

		/****************************************************************************************************
		 * Verification Routines returning a BOOLEAN
		 * **************************************************************************************************/

		/// <summary>
		/// Tuned
		/// </summary>
		/// <param name="EmailStr">String to be tested as email</param>
		/// <returns></returns>
		public static bool IsValidEmail(string EmailStr)
		{
			if (EmailStr.Length==0) return false;
			bool GFCheckEmail=true;
			int dotPos=-1, atPos=EmailStr.IndexOf("@", StringComparison.Ordinal);
			if (atPos>=0)
				dotPos=EmailStr.IndexOf(".",atPos, StringComparison.Ordinal);
			if (atPos<0 || dotPos<0)
				GFCheckEmail=false;
			return GFCheckEmail;
		}
		/// <summary>
		/// Tuned
		/// </summary>
		/// <param name="typ"></param>
		/// <returns></returns>
		public static bool IsIntegerFld(string typ)
		{
            if (typ.Equals("int") || typ.Equals("tinyint") || typ.Equals("decimal") || typ.Equals("smallint") || typ.Equals("bigint") || typ.Equals("System.Byte") || typ.Equals("System.Int32") || typ.Equals("System.Int64"))
				return true;
			else
				return false;
		}

		public static bool IsFloatFld(string typ)
		{
			if (typ.Equals("money") || typ.Equals("float") || typ.Equals("numeric") || typ.Equals("real") || typ.Equals("smallmoney") || typ.Equals("System.Double")) 
				return true;
			else
				return false;
		}
	
		///// <summary>
		///// Tuned
		///// </summary>
		///// <param name="inStr"></param>
		///// <returns></returns>
		//public static bool IsDate(string inStr)
		//{
		//	if (inStr.Length==0) return false;
		//	bool ret=false;
		//	try
		//	{
  //              var a = DateTime.Parse(inStr, CultureInfo.InvariantCulture);
		//		ret=true;
		//	}
		//	catch { ret=false; }
		//	return ret;
		//}
		/// <summary>
		/// Tuned
		/// </summary>
		/// <param name="dateA"></param>
		/// <param name="dateB"></param>
		/// <returns></returns>
		public static bool IsLaterThan(string dateA, string dateB)
		{
			if (dateA.Length==0) return false;
			if (dateB.Length==0) return true;
			bool ret=false;
			try
			{
#if JCNOPROFILE
int a=System.DateTime.Compare(System.DateTime.Parse(dateA),System.DateTime.Parse(dateB));
#endif
                if (DateTimeOffset.Compare(TheCommonUtils.CDate(dateA), TheCommonUtils.CDate(dateB)) > 0)
					ret=true;
			}
			catch { ret=false; }
			return ret;
		}
        ///// <summary>
        ///// Tuned
        ///// </summary>
        ///// <param name="inStr"></param>
        ///// <returns></returns>
        //public static bool IsNumeric(string inStr)
        //{
        //	if (inStr.Length==0) return false;
        //	bool retVal;
        //	try
        //	{
        //		int test=int.Parse(inStr);
        //		retVal=true;
        //	}
        //	catch (Exception)
        //	{
        //		retVal=false;
        //	}
        //	return retVal;
        //}
        ///// <summary>
        ///// Tuned
        ///// </summary>
        ///// <param name="inStr"></param>
        ///// <returns></returns>
        //public static bool IsFloat(string inStr)
        //{
        //	if (inStr.Length==0) return false;
        //	bool retVal;
        //	try
        //	{
        //		double test=double.Parse(inStr);
        //		retVal=true;
        //	}
        //	catch (Exception)
        //	{
        //		retVal=false;
        //	}
        //	return retVal;
        //}
        #endregion

        private readonly TheMirrorCache<TheBlobData> MyBlobCache=null;
        public string MyConnectionString;
        public string[] SQLTypeNames = { "[nvarchar](4000)", "[nvarchar](512)", "[int]", "[float]", "[datetime2]", "[bit]", "[uniqueidentifier]", "[float]", "[bigint]", "[tinyint]", "[nvarchar](1)", "[nvarchar](max)", "[varbinary](max)", "[int]", "[smallint]" };
        internal string thingStoreUniqueID = TheStorageUtilities.GenerateUniqueIDFromType(typeof(TheThingStore), null);

        private readonly int mRetrySeconds=50;
        private readonly int mRetries = 3;

        /********************************************************************************************
		 * SQL Helper Functions
		 ********************************************************************************************/
        public bool DoesTableExist(string tablename)
        {
            Guid pConnKey = Guid.Empty;
            if (TheCommonUtils.CLng(cdeGetScalarData("SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME='" + tablename + "'", 2, false,false, ref pConnKey)) == 1)
                return true;
            else
                return false;
        }
        public bool DoesColumnExist(string TableName, string ColumnName)
        {
            Guid pConnKey = Guid.Empty;
            if (TheCommonUtils.CLng(cdeGetScalarData("SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='" + TableName + "' AND COLUMN_NAME='" + ColumnName + "'", 2, false, false, ref pConnKey)) == 1)
                return true;
            else
                return false;
        }

        //MySqlHelperClass.CopyTable(MySqlHelperClass.MyConnectionString, "cdeStore8", "cdeStore15");
        private readonly object CopyTableLock=new object();
        public void cdeCopyTable(string _sourceConnectionString, string pSourcetable, string mDestTable)
        {
            if (TheCommonUtils.cdeIsLocked(CopyTableLock)) return;
            lock (CopyTableLock)
            {
                string sql = string.Format("SELECT * FROM [{0}]", pSourcetable);

                SqlDataReader dr = cdeOpenDataReader(_sourceConnectionString, sql, 0);
                Guid tConn = Guid.Empty;
                if (dr != null)
                {
                    do
                    {
                        sql = "insert into " + mDestTable + " (";
                        string sqlVal = ") VALUES (";
                        bool IsFirst = true;
                        for (int i = 0; i < dr.FieldCount; i++)
                        {
                            string fn = "";
                            switch (dr.GetName(i))
                            {
                                case "cdeIDX":
                                    continue;
                                default:
                                    fn = dr.GetName(i);
                                    break;
                            }
                            if (IsFirst)
                                IsFirst = false;
                            else
                            { sql += ","; sqlVal += ","; }
                            sql += fn;
                            sqlVal += "'" + dr[i] + "'";
                        }
                        sql += sqlVal + ")";
                        cdeRunNonQuery(MyConnectionString, sql, 0, true, true, ref tConn);
                    } while (dr.Read());
                    dr.Close();
                }
                cdeCloseConnection(ref tConn);
            }
        }


	    /// <summary>
	    /// 
	    /// </summary>
	    /// <param name="pSQL"></param>
	    /// <param name="pFlags">
	    /// 1 = No Primary Key necessary
	    /// 2 = Cache Result Set
	    /// 4 = Force Cache even if DS is null
	    /// </param>
	    /// <param name="pTimeout"></param>
	    /// <param name="bUseOpen"></param>
	    /// <param name="bKeepOpen"></param>
	    /// <param name="pConnKey"></param>
	    /// <returns></returns>
	    public DataSet cdeOpenDataSet(string pSQL, int pFlags,int pTimeout, bool bUseOpen, bool bKeepOpen, ref Guid pConnKey)
		{
			SqlDataAdapter tSQLADA=null;
            return cdeOpenDataSet("", pSQL, null, out tSQLADA, "", pFlags, pTimeout, bUseOpen, bKeepOpen, ref pConnKey);
		}
        public DataSet cdeOpenDataSet(string pCustomDB, string pSQL, string tableName, int pFlags, bool bUseOpen, bool bKeepOpen)
		{
			SqlDataAdapter tSQLADA=null;
            Guid pConnKey = Guid.Empty;
            return cdeOpenDataSet(pCustomDB, pSQL, null, out tSQLADA, tableName, pFlags, 0, bUseOpen, bKeepOpen, ref pConnKey);
		}
        public DataSet cdeOpenDataSet(string pSQL, out SqlDataAdapter pSqlDataAdapter, int pFlags, bool bUseOpen, bool bKeepOpen)
		{
            Guid pConnKey = Guid.Empty;
            return cdeOpenDataSet("", pSQL, null, out pSqlDataAdapter, "", pFlags, 0, bUseOpen, bKeepOpen, ref pConnKey);
		}
        public DataSet cdeOpenDataSet(string pCustomDB, string pSQL, out SqlDataAdapter pSqlDataAdapter, string tableName, int pFlags, bool bUseOpen, bool bKeepOpen)
		{
            Guid pConnKey = Guid.Empty;
            return cdeOpenDataSet(pCustomDB, pSQL, null, out pSqlDataAdapter, tableName, pFlags, 0, bUseOpen, bKeepOpen, ref pConnKey);
		}
        public DataSet cdeOpenDataSet(string pCustomDB, string pSQL, out SqlDataAdapter pSqlDataAdapter, string tableName, int pFlags, int TimeOut, bool bUseOpen, bool bKeepOpen)
		{
            Guid pConnKey = Guid.Empty;
            return cdeOpenDataSet(pCustomDB, pSQL, null, out pSqlDataAdapter, tableName, pFlags, 0, bUseOpen, bKeepOpen, ref pConnKey);
		}

	    /// <summary>
	    /// Opens a SQL Dataset
	    /// </summary>
	    /// <param name="pCustomDB"></param>
	    /// <param name="pSQL"></param>
	    /// <param name="inDS"></param>
	    /// <param name="pSqlDataAdapter"></param>
	    /// <param name="tableName"></param>
	    /// <param name="pFlags">
	    /// 1 = No Primary Key necessary
	    /// 2 = Cache Result Set
	    /// 4 = Force Cache even if DS is null
	    /// 8 = New in 3.5.43: Structure Only Return
	    /// 16 = FlashCache: 5 Seconds Cache
	    /// </param>
	    /// <param name="TimeOut"></param>
	    /// <param name="bUseOpen"></param>
	    /// <param name="bKeepOpen"></param>
	    /// <param name="pConnKey"></param>
	    /// <returns></returns>
	    public DataSet cdeOpenDataSet(string pCustomDB,string pSQL,DataSet inDS,out SqlDataAdapter pSqlDataAdapter,string tableName,int pFlags,int TimeOut,bool bUseOpen,bool bKeepOpen, ref Guid pConnKey)
		{
			DataSet myDataSet = inDS ?? new DataSet();
		    pSqlDataAdapter=null;
            if ((pFlags & 18) != 0)
            {
                //New Caching
                TheBlobData tBlob = MyBlobCache.GetEntryByID("DS:" + pSQL);
                if (tBlob != null)
                    return tBlob.BlobObject as DataSet;
            }
            else
            {
                MyBlobCache.RemoveAnItemByKey("DS:" + pSQL, null);
            }

			if (tableName.Length==0) tableName="CDEDS";
            if (pCustomDB.Length == 0) pCustomDB = TheCommonUtils.CStr(MyConnectionString);
            try
            {
                //SqlConnection  myConnection = new SqlConnection(pCustomDB);
                TheBlobData tConn = new TheBlobData();
                if (!bUseOpen || pConnKey == Guid.Empty)
                {
                    tConn.BlobObject = new SqlConnection(pCustomDB);
                    tConn.cdeEXP = TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout;

                    pConnKey = Guid.NewGuid();
                    MyBlobCache.AddOrUpdateItem(pConnKey, tConn, null);
                }
                else
                {
                    tConn = MyBlobCache.GetEntryByID(pConnKey);
                    if (tConn == null)
                    {
                        AppendError("OpenDataSet", "No matching Connection found");
                        return null;
                    }
                }

                pSqlDataAdapter = new SqlDataAdapter(pSQL, tConn.BlobObject as SqlConnection);
                if (TimeOut > 0)
                {
                    pSqlDataAdapter.SelectCommand.CommandTimeout = TimeOut;
                }
                if ((pFlags & 1) == 0)
                    pSqlDataAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                if ((pFlags & 8) != 0)
                    pSqlDataAdapter.FillSchema(myDataSet, SchemaType.Source, tableName);
                else
                    pSqlDataAdapter.Fill(myDataSet, tableName);
                if (((pFlags & 18) != 0 || MyBlobCache.ContainsID("DS:" + pSQL))) // Sets caching objects
                {
                    TheBlobData tCache = new TheBlobData();
                    if ((pFlags & 16) != 0)
                        tCache.cdeEXP = 3;
                    tCache.BlobObject = myDataSet;
                    MyBlobCache.AddOrUpdateItemKey("DS:" + pSQL,tCache,null);
                }
            }
            catch (Exception e)
            {
                AppendError("Dataset Open on " + pSQL, e.ToString());
                if (inDS == null) myDataSet = null;
            }
            finally
            {
                // Close the connection when done with it.
                if (!bKeepOpen)
                    cdeCloseConnection(ref pConnKey);
            }
			return myDataSet;
		}

        public void cdeUpdateDS(SqlDataAdapter tSQLAda, DataSet ttRS, string TableName, ref Guid pConnKey)
		{
			cdeUpdateDS(tSQLAda,ttRS,TableName,true,ref pConnKey);
		}
		public void cdeUpdateDS(SqlDataAdapter tSQLAda,DataSet ttRS,string TableName,bool bClose,ref Guid pConnKey)
		{
			try 
			{
				tSQLAda.Update(ttRS,TableName);
				if (bClose)
				{
					tSQLAda.Dispose();
					cdeCloseConnection(ref pConnKey);
				}
			}
			catch (DBConcurrencyException e)
			{
				AppendError("cdeUpdateDS","Exception occured:\n"+ e +"\nTrying to Update Table ["+ TableName +"]\nSQLCommand:"+ tSQLAda.SelectCommand.CommandText +"<HR>Row Effected:"+ e.Row[0]);
			}
		}

        public void cdeCloseConnection(ref Guid pConnKey)
        {
            if (pConnKey== Guid.Empty) return;
            TheBlobData tConn = MyBlobCache.GetEntryByID(pConnKey);
            SqlConnection tSqlConn = tConn?.BlobObject as SqlConnection;
            if (tSqlConn != null && tSqlConn.State == ConnectionState.Open)
            {
                try
                {
                    tSqlConn.Close();
                }
                catch
                {
                    // ignored
                }
            }
            tConn = null;
            MyBlobCache.RemoveAnItemByID(pConnKey,null);
            pConnKey = Guid.Empty;
        }

        /// <summary>
        /// Tuned
        /// </summary>
        /// <param name="pSQL"></param>
        /// <param name="pTimeout"></param>
        /// <param name="UseOpen"></param>
        /// <param name="KeepOpen"></param>
        /// <param name="pConnKey"></param>
        public bool cdeRunNonQuery(string pSQL,int pTimeout, bool UseOpen,bool KeepOpen,ref Guid pConnKey)
		{
			if (pSQL.Length==0) return false;
            return cdeRunNonQuery(MyConnectionString, pSQL, pTimeout, UseOpen, KeepOpen, ref pConnKey);
		}
        public bool cdeRunNonQuery(string pSQL, bool UseOpen, bool KeepOpen, ref Guid pConnKey)
        {
            if (pSQL.Length == 0) return false;
            return cdeRunNonQuery(MyConnectionString, pSQL, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout, UseOpen, KeepOpen, ref pConnKey);
        }
        public bool cdeRunNonQuery(string pSQL, int timeOut)
		{
			if (pSQL.Length==0) return false;
            Guid pConnKey = Guid.Empty;
			return cdeRunNonQuery(MyConnectionString,pSQL,timeOut,false,false,ref pConnKey);
		}
		public bool cdeRunNonQuery(string pCustomDB, string pSQL)
		{
            if (pSQL.Length == 0 || pCustomDB.Length==0) return false;
            Guid pConnKey = Guid.Empty;
            return cdeRunNonQuery(pCustomDB, pSQL, 0, false, false, ref pConnKey);
		}

	    /// <summary>
	    /// Runs a query without any result paramerter 
	    /// </summary>
	    /// <param name="pCustomDB"></param>
	    /// <param name="pSQL"></param>
	    /// <param name="timeOut"></param>
	    /// <param name="UseOpen"></param>
	    /// <param name="KeepOpen"></param>
	    /// <param name="pConnKey"></param>
	    public bool cdeRunNonQuery(string pCustomDB, string pSQL, int timeOut, bool UseOpen, bool KeepOpen, ref Guid pConnKey)
		{
			if (pSQL.Length==0) return false;
			bool ret=true;
		    TheBlobData mySqlConnection = new TheBlobData();

            if (pCustomDB.Length == 0) pCustomDB = TheCommonUtils.CStr(MyConnectionString);

            try
            {
                if (!UseOpen || pConnKey== Guid.Empty)
                {
                    mySqlConnection.BlobObject = new SqlConnection(pCustomDB);
                    mySqlConnection.cdeEXP = TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout;
                    ((SqlConnection)mySqlConnection.BlobObject).Open();
                    pConnKey = Guid.NewGuid();
                    MyBlobCache.AddOrUpdateItem(pConnKey, mySqlConnection,null);
                }
                else
                {
                    mySqlConnection=MyBlobCache.GetEntryByID(pConnKey);
                    if (mySqlConnection == null)
                    {
                        AppendError("cdeRunNonQuery", "No matching Connection found");
                        return false;
                    }
                }
                var mySqlCommand = new SqlCommand(pSQL, mySqlConnection.BlobObject as SqlConnection);
                if (timeOut > 0) mySqlCommand.CommandTimeout = timeOut;
                mySqlCommand.ExecuteNonQuery();
#if JCDEBUG
if (CInt(Application["DBG"])>0)	ResponseEmail(null,"SQL-RNQ: "+ pSQL,1);
#endif
            }
            catch (Exception e)
            {
                AppendError("cdeRunNonQuery", e.ToString());
                ret = false;
            }
            finally
            {
                // Close the connection when done with it.
                if (!KeepOpen)
                    cdeCloseConnection(ref pConnKey);
            }
			return ret;
		}

		/// <summary>
		/// Runs a query that return a single column and row
		/// Tuned
		/// </summary>
		/// <param name="pSQL">Sql commant</param>
		/// <param name="pFlags">
		/// 2=Cache Result
		/// 4=Cache even nulls
		/// </param>
        /// <param name="UseOpen">Use an open connection</param>
        /// <param name="pKeepOpen">Dont close the connection after using it</param>
        /// <param name="pConnKey">Use an existing SQL Connection</param>
		/// <returns></returns>
        public object cdeGetScalarData(string pSQL, int pFlags, bool UseOpen,bool pKeepOpen, ref Guid pConnKey)
		{
			if (pSQL==null) return null;
            return cdeGetScalarData(MyConnectionString, pSQL, pFlags, UseOpen,pKeepOpen, ref pConnKey);
		}
		/// <summary>
		/// V31: Bringup done
		/// </summary>
		/// <param name="pCustomDB">Custom Connection String</param>
		/// <param name="pSQL">SQL clause</param>
		/// <param name="pFlags">
		/// 2=Cache Result
        /// 4 = Force Cache even if result is null
        /// 16=Cache result but only for 3 seconds
		/// </param>
        /// <param name="UseOpen">Use an open connection</param>
        /// <param name="bKeepOpen">Dont close the connection after using it</param>
        /// <param name="pConnKey">Use an existing SQL Connection</param>
        /// <returns></returns>
        public object cdeGetScalarData(string pCustomDB, string pSQL, int pFlags, bool UseOpen, bool bKeepOpen, ref Guid pConnKey)
        {
            if (pSQL.Length == 0) return null;
            if ((pFlags & 18) != 0)
            {
                //New Caching
                TheBlobData tBlob = MyBlobCache.GetEntryByID("SD:" + pSQL);
                if (tBlob != null)
                {
                    if (tBlob.cdeEXP == 0 || DateTimeOffset.Now.Subtract(tBlob.cdeCTIM).TotalSeconds < tBlob.cdeEXP)
                        return tBlob.BlobObject;
                    else
                        MyBlobCache.RemoveAnItemByKey("SD:" + pSQL, null);
                }
            }
            else
                MyBlobCache.RemoveAnItemByKey("SD:" + pSQL, null);

            object foo = null;
            TheBlobData mySqlConnection = new TheBlobData();

            if (pCustomDB.Length == 0) pCustomDB = TheCommonUtils.CStr(MyConnectionString);

            try
            {
                if (!UseOpen || pConnKey==Guid.Empty)
                {
                    mySqlConnection.BlobObject = new SqlConnection(pCustomDB);
                    mySqlConnection.cdeEXP = TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout;
                    ((SqlConnection)mySqlConnection.BlobObject).Open();
                    pConnKey = Guid.NewGuid();
                    MyBlobCache.AddOrUpdateItem(pConnKey, mySqlConnection,null);
                }
                else
                {
                    mySqlConnection=MyBlobCache.GetEntryByID(pConnKey);
                    if (mySqlConnection == null)
                    {
                        AppendError("cdeGetScalarData", "No matching Connection found");
                        return false;
                    }
                }
                var mySqlCommand = new SqlCommand(pSQL, mySqlConnection.BlobObject as SqlConnection) {CommandTimeout = TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout};
                foo = mySqlCommand.ExecuteScalar();
#if JCDEBUG
if (CInt(Application["DBG"])>0)	ResponseEmail(null,"SQL-GSD: "+ pSQL,1);
#endif
                if ((foo != null || (pFlags & 4) != 0) && ((pFlags & 18) != 0 || MyBlobCache.ContainsID("SD:" + pSQL))) // Sets caching objects
                {
                    TheBlobData tCache = new TheBlobData();
                    if ((pFlags & 16) != 0)
                        tCache.cdeEXP = 3;
                    tCache.BlobObject = foo;
                    MyBlobCache.AddOrUpdateItemKey("SD:" + pSQL,tCache,null);
                }
            }
            catch (Exception e)
            {
                AppendError("cdeGetScalarData", e.ToString());
                foo = null;
            }
            finally
            {
                // Close the connection when done with it.
                if (!bKeepOpen)
                    cdeCloseConnection(ref pConnKey);
            }
            return foo;
        }


	    /// <summary>
	    /// Open a Data Reader and returns it
	    /// Tuned
	    /// </summary>
	    /// <param name="pSQL">1=return only a single row</param>
	    /// <param name="pFlags"></param>
	    /// <returns></returns>
	    public SqlDataReader cdeOpenDataReader(string pSQL,int pFlags) //,out bool bEOF)
		{
			if (pSQL.Length==0) return null;
            return cdeOpenDataReader(TheCommonUtils.CStr(MyConnectionString), pSQL, pFlags, 0);
		}
		public SqlDataReader cdeOpenDataReader(string pSQL,int pFlags,int timeOut) //,out bool bEOF)
		{
			if (pSQL.Length==0) return null;
            return cdeOpenDataReader(TheCommonUtils.CStr(MyConnectionString), pSQL, pFlags, timeOut);
		}
		public SqlDataReader cdeOpenDataReader(string strCon,string pSQL,int pFlags)
		{
			if (pSQL.Length==0) return null;
			return cdeOpenDataReader(strCon,pSQL,pFlags,0);
		}

	    /// <summary>
	    /// Returns a DataReader
	    /// Tuned
	    /// </summary>
	    /// <param name="strCon"></param>
	    /// <param name="pSQL"></param>
	    /// <param name="pFlags">
	    /// 1=Returns only a single Row
	    /// </param>
	    /// <param name="timeOut"></param>
	    /// <returns></returns>
	    public SqlDataReader cdeOpenDataReader(string strCon,string pSQL,int pFlags,int timeOut)
		{
			if (pSQL.Length==0) return null;
			SqlDataReader drSSE=null;
		    SqlConnection tSqlConnection = null;

            if (strCon.Length == 0) strCon = TheCommonUtils.CStr(MyConnectionString);

			try
			{
				tSqlConnection = new SqlConnection(strCon);
				tSqlConnection.Open();
				var mySqlCommand = new SqlCommand(pSQL, tSqlConnection);
				if (timeOut>0) mySqlCommand.CommandTimeout=timeOut;
				drSSE = (pFlags&1)!=0 ? mySqlCommand.ExecuteReader(CommandBehavior.CloseConnection|CommandBehavior.SingleRow) : mySqlCommand.ExecuteReader(CommandBehavior.CloseConnection);
				if (!drSSE.Read()) 
				{
					drSSE.Close();
					drSSE=null;
				}
#if JCDEBUG
if (CInt(Application["DBG"])>0)	ResponseEmail(null,"SQL-DR: "+ pSQL,1);
#endif
			}
			catch(Exception e)
			{
                AppendError("cdeOpenDataReader", e.ToString());
				drSSE=null;
				if (tSqlConnection != null && tSqlConnection.State == ConnectionState.Open)
					tSqlConnection.Close();
			}
			return drSSE;
		}

		/// <summary>
		/// Closes the datareader above
		/// Tuned
		/// </summary>
		/// <param name="pDRTemp"></param>
		public void cdeCloseDataReader(SqlDataReader pDRTemp)
		{
			if (pDRTemp==null) return;
			try 
			{
				if (pDRTemp.IsClosed==false)
					pDRTemp.Close();
			}
		    catch
		    {
		        // ignored
		    }
		}

        private TheMirrorCache<TheInsertQueueItem> MyInsertQueue;
        private readonly object cdeUpdateInsertRecordLock = new object();

        public object MyServiceHostInfo { get; private set; }

        public void cdeUpdateInsertRecord(TheInsertQueueItem pItem)
        {
            //if (InUpdate)
            if (TheCommonUtils.cdeIsLocked(cdeUpdateInsertRecordLock))
            {
                if (MyInsertQueue != null)
                {
                    MyInsertQueue.AddOrUpdateItem(Guid.NewGuid(), pItem, null);
                    TheBaseAssets.MySYSLOG.WriteToLog(480, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("SQLHelpers", "Add Was Queued - Current QueueLength=" + MyInsertQueue.Count, eMsgLevel.l4_Message));
                }
                return;
            }

            lock (cdeUpdateInsertRecordLock)
            {
                if (MyInsertQueue == null)
                    MyInsertQueue = new TheMirrorCache<TheInsertQueueItem>(0);

                MyInsertQueue.AddOrUpdateItem(Guid.NewGuid(), pItem, null); 

                while (MyInsertQueue.Count > 0 && TheBaseAssets.MasterSwitch)
                {
                    Guid MyQItemKey =Guid.Empty;
                    TheInsertQueueItem MyQItem = MyInsertQueue.GetFirstItem(out MyQItemKey);
                    bool doRemoveItem = true;
                    if (MyQItem == null || MyQItemKey==Guid.Empty) break;
                    try
                    {
                        string tUpdatedMids = "";
                        int primkeyFld = -1;
                        for (int i = 0; i < MyQItem.DataStoreGramm.FLDs.Count; i++)
                        {
                            if (MyQItem.DataStoreGramm.FLDs[i].N.ToUpper().Equals("CDEMID"))
                            {
                                primkeyFld = i;
                                break;
                            }
                        }
                        foreach (List<string> tDataGramm in MyQItem.DataStoreGramm.RECs)
                        {
                            Guid tConnKey = Guid.NewGuid();

                            bool IsUpdate = (MyQItem.DataStoreGramm.CMD == eSCMD.Update || MyQItem.DataStoreGramm.CMD == eSCMD.InsertOrUpdate);
                            string SQLcreate2 = "";
                            Guid PrimaryKey = Guid.Empty;
                            int ColNo = 0;
                            string tVal = "";
                            bool InsertMID = false;

                            if (IsUpdate && primkeyFld >= 0)
                            {
                                string k = TheStorageUtilities.GetValueByCol(tDataGramm, primkeyFld);
                                PrimaryKey = TheCommonUtils.CGuid(k);
                                if (!PrimaryKey.Equals(Guid.Empty))
                                {
                                    IsUpdate = TheCommonUtils.CGuid(cdeGetScalarData(string.Format("select top 1 cdeMID from [dbo].[" + (MyQItem.DataStoreGramm.UID.Equals(CommonTypeUIDs.cdePUniqueID) ? "cdeProperties" : "cdeStore") + "{0}] where cdeMID='{1}'", MyQItem.RealStoreID, PrimaryKey.ToString()), 0, false, false, ref tConnKey)) != Guid.Empty;
                                    TheBaseAssets.MySYSLOG.WriteToLog(481, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("SQLHelpers", string.Format("cdeMID {0} used. Will Update: {1}", PrimaryKey.ToString(), IsUpdate), eMsgLevel.l7_HostDebugMessage), true);
                                }
                                else
                                {
                                    if (MyQItem.DataStoreGramm.CMD == eSCMD.Update)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(482, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("SQLHelpers", string.Format("Illegal formated cdeMID: {0} - update not possible", k), eMsgLevel.l7_HostDebugMessage), true);
                                        continue;
                                    }
                                    TheBaseAssets.MySYSLOG.WriteToLog(483, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("SQLHelpers", string.Format("Illegal formated cdeMID: {0} inserting", k), eMsgLevel.l7_HostDebugMessage), true);
                                    IsUpdate = false;
                                }
                            }
                            else
                            {
                                if (MyQItem.DataStoreGramm.CMD == eSCMD.Update)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(484, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("SQLHelpers", "cdeMID NOT found! Update not possible", eMsgLevel.l7_HostDebugMessage), true);
                                    continue;
                                }
                                if (IsUpdate)
                                    TheBaseAssets.MySYSLOG.WriteToLog(485, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("SQLHelpers", "cdeMID NOT found! Inserting... ", eMsgLevel.l7_HostDebugMessage), true);
                                IsUpdate = false;
                            }

                            if (IsUpdate)
                            {
                                SQLcreate2 = "update [dbo].[" + (MyQItem.DataStoreGramm.UID.Equals(CommonTypeUIDs.cdePUniqueID) ? "cdeProperties" : "cdeStore") + MyQItem.RealStoreID + "] set ";
                                if (!string.IsNullOrEmpty(MyQItem.SScopeID))
                                    SQLcreate2 += "cdeSCOPEID='" + TheScopeManager.GetTokenFromScrambledScopeID(MyQItem.SScopeID) + "',"; //MyQItem.SScopeID + "',";
                                int fldCnt = 0;
                                foreach (string rtVal in tDataGramm)
                                {
                                    ColNo = TheCommonUtils.CInt(rtVal.Substring(0, 3));
                                    tVal = rtVal.Substring(3);
                                    if (!MyQItem.DataStoreGramm.FLDs[ColNo].N.ToUpper().Equals("CDEMID") && tVal.Length > 0)
                                    {
                                        if (fldCnt > 0) SQLcreate2 += ",";
                                        SQLcreate2 += MyQItem.DataStoreGramm.FLDs.Find(s => s.C == ColNo).N;
                                        SQLcreate2 += "=@fld" + ColNo;
                                        fldCnt++;
                                    }
                                }
                                SQLcreate2 += string.Format(" where cdeMID='{0}'", PrimaryKey);
                                if (tUpdatedMids.Length > 0) tUpdatedMids += ";";
                                tUpdatedMids += PrimaryKey;
                            }
                            else
                            {
                                SQLcreate2 = "insert into [dbo].[" + (MyQItem.DataStoreGramm.UID.Equals(CommonTypeUIDs.cdePUniqueID) ? "cdeProperties" : "cdeStore") + MyQItem.RealStoreID + "] (";
                                if (!string.IsNullOrEmpty(MyQItem.SScopeID))
                                    SQLcreate2 += "cdeSCOPEID,";
                                int fldCnt = 0;
                                foreach (string rtVal in tDataGramm)
                                {
                                    tVal = rtVal.Substring(3);
                                    ColNo = TheCommonUtils.CInt(rtVal.Substring(0, 3));

                                    if (tVal.Length > 0)
                                    {
                                        if (MyQItem.DataStoreGramm.FLDs[ColNo].N.ToUpper().Equals("CDEMID"))
                                        {
                                            if (TheCommonUtils.CGuid(tVal) == Guid.Empty) continue;
                                            InsertMID = true;
                                            PrimaryKey = new Guid(tVal);
                                        }
                                        if (fldCnt > 0) SQLcreate2 += ",";
                                        SQLcreate2 += MyQItem.DataStoreGramm.FLDs.Find(s => s.C == ColNo).N;
                                        fldCnt++;
                                    }
                                }
                                SQLcreate2 += ") values (";
                                if (!string.IsNullOrEmpty(MyQItem.SScopeID))
                                    SQLcreate2 += "'" + TheScopeManager.GetTokenFromScrambledScopeID(MyQItem.SScopeID) + "',"; //MyQItem.SScopeID + "',";
                                fldCnt = 0;
                                foreach (string rtVal in tDataGramm)
                                {
                                    tVal = rtVal.Substring(3);
                                    ColNo = TheCommonUtils.CInt(rtVal.Substring(0, 3));
                                    if ((!MyQItem.DataStoreGramm.FLDs[ColNo].N.ToUpper().Equals("CDEMID") || InsertMID) && tVal.Length > 0)
                                    {
                                        if (fldCnt > 0) SQLcreate2 += ",";
                                        SQLcreate2 += "@fld" + ColNo;
                                        fldCnt++;
                                    }
                                }
                                SQLcreate2 += ")";
                            }

                            tConnKey = Guid.NewGuid();
                            for (Int32 attempt = 1; ; )
                            {
                                try
                                {
                                    TheBlobData mySqlConnection = new TheBlobData
                                    {
                                        cdeEXP = TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout,
                                        BlobObject = new SqlConnection(MyConnectionString)
                                    };
                                    ((SqlConnection)mySqlConnection.BlobObject).Open();
                                    MyBlobCache.AddOrUpdateItem(tConnKey, mySqlConnection, null);
                                    SqlCommand command = new SqlCommand(SQLcreate2, mySqlConnection.BlobObject as SqlConnection);
                                    foreach (string rtVal in tDataGramm)
                                    {
                                        tVal = rtVal.Substring(3);
                                        ColNo = TheCommonUtils.CInt(rtVal.Substring(0, 3));
                                        if ((!MyQItem.DataStoreGramm.FLDs[ColNo].N.ToUpper().Equals("CDEMID") || InsertMID) && tVal.Length > 0)
                                        {
                                            SqlParameter paramTitle = null;
                                            switch (MyQItem.DataStoreGramm.FLDs[ColNo].T)
                                            {
                                                case "System.String":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.NVarChar, 512) {Value = tVal};
                                                    break;
                                                case "System.Boolean":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.Bit) {Value = TheCommonUtils.CBool(tVal)};
                                                    break;
                                                case "System.Single":
                                                case "System.Double":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.Float) {Value = TheCommonUtils.CDbl(tVal)};
                                                    break;
                                                case "System.Int16":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.SmallInt) {Value = TheCommonUtils.CShort(tVal)};
                                                    break;
                                                case "System.Int32":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.Int) {Value = TheCommonUtils.CInt(tVal)};
                                                    break;
                                                case "System.Int64":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.BigInt) {Value = TheCommonUtils.CLng(tVal)};
                                                    break;
                                                case "System.DateTime":
                                                    //paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.DateTime2) {Value = TheCommonUtils.CDate(tVal)};    //THIS FAILS as CDate always returns DateTimeOffset
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.DateTimeOffset) { Value = TheCommonUtils.CDate(tVal) };    
                                                    break;
                                                case "System.DateTimeOffset":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.DateTimeOffset) {Value = TheCommonUtils.CDate(tVal)};
                                                    break;
                                                case "System.Guid":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.UniqueIdentifier) {Value = TheCommonUtils.CGuid(tVal)};
                                                    break;
                                                case "System.Byte":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.TinyInt) {Value = TheCommonUtils.CByte(tVal)};
                                                    break;
                                                case "System.Char":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.NVarChar, 1) {Value = TheCommonUtils.CChar(tVal)};
                                                    break;
                                                case "System.Byte[]":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.VarBinary, -1) {Value = Convert.FromBase64String(tVal)};
                                                    break;
                                                case "System.Char[]":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.NVarChar, -1) {Value = tVal};
                                                    break;
                                                case "System.Nullable`1[System.Int64]":
                                                    object nullableVal = tVal;
                                                    if (tVal != null)
                                                        nullableVal = TheCommonUtils.CLng(tVal);
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.BigInt) { Value = nullableVal };
                                                    break;
                                                case "System.Nullable`1[System.Guid]":
                                                    object nullableGuid = tVal;
                                                    if (tVal != null)
                                                        nullableVal = TheCommonUtils.CGuid(tVal);
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.UniqueIdentifier) { Value = nullableGuid };
                                                    break;
                                                case "nsCDEngine.ViewModels.cdeConcurrentDictionary`2[System.String,nsCDEngine.Engines.ThingService.cdeP]":
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.NVarChar, -1) { Value = tVal };
                                                    break;
                                                default:
                                                    paramTitle = new SqlParameter("@fld" + ColNo, SqlDbType.NVarChar, 4000) {Value = tVal};
                                                    break;
                                            }
                                            if (paramTitle != null)
                                                command.Parameters.Add(paramTitle);
                                        }
                                    }
                                    command.ExecuteNonQuery();
                                }
                                catch (SqlException sqlException)
                                {
                                    // Increment Trys
                                    attempt++;

                                    // Throw Error if we have reach the maximum number of retries
                                    if (attempt == mRetries)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(4421, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SQLHelper", $"SQL Update/Insert - maximum retries failed on: {SQLcreate2.Substring(20)}", eMsgLevel.l1_Error, sqlException.ToString()));
                                        AppendError("SQL Update/Insert - maximum retries failed on: " + SQLcreate2, sqlException.ToString());
                                        break;
                                    }

                                    // Determine if we should retry or abort.
                                    if (!RetryLitmus(sqlException))
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(4422, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SQLHelper", $"SQL Update/Insert - No retries due to fatal SQL error on: {SQLcreate2.Substring(20)}", eMsgLevel.l1_Error, sqlException.ToString()));
                                        break;
                                    }
                                    else
                                    {
                                        doRemoveItem = false;
                                        Thread.Sleep(ConnectionRetryWaitSeconds(attempt));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(4423, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SQLHelper", $"SQL Update/Insert error on: {SQLcreate2}", eMsgLevel.l1_Error, ex.ToString()));
                                    AppendError("SQL Update/Insert error on: " + SQLcreate2, ex.ToString());
                                }
                                if (!IsUpdate && primkeyFld >= 0)
                                {
                                    string tPID = "";
                                    tPID = InsertMID ? PrimaryKey.ToString() : TheCommonUtils.CGuid(cdeGetScalarData("SELECT top 1 cdeMID FROM [dbo].[cdeStore" + MyQItem.RealStoreID + "]", 0, true, true, ref tConnKey)).ToString();
                                    if (!string.IsNullOrEmpty(MyQItem.DataStoreGramm.MID) || MyQItem.LocalCallback != null)
                                        tDataGramm[primkeyFld] = string.Format("{0:000}{1}", primkeyFld, tPID);
                                    else
                                    {
                                        if (tUpdatedMids.Length > 0) tUpdatedMids += ";";
                                        tUpdatedMids += tPID;
                                    }
                                }
                                cdeCloseConnection(ref tConnKey);
                                break;
                            }
                        }

                        string res = MyQItem.DataStoreGramm.UID;
                        if (!string.IsNullOrEmpty(MyQItem.DataStoreGramm.MID) || MyQItem.LocalCallback != null)
                        {
                            res = TheCommonUtils.SerializeObjectToJSONString(MyQItem.DataStoreGramm);
                        }
                        else
                        {
                            if (tUpdatedMids.Length > 0)
                                res += ":" + tUpdatedMids;
                        }
                        //InUpdate = false;
                        TSM SendToClients = new TSM(TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineName(), "DATAWASUPDATED:" + MyQItem.DataStoreGramm.MID, res);
                        if (!MyQItem.LocalCallbackOnly)
                        {
                            if (MyQItem.DirectTarget!=Guid.Empty && MyQItemKey!=Guid.Empty)
                                TheCommCore.PublishToNode(MyQItem.DirectTarget, MyQItem.SScopeID, SendToClients);
                            //TheCommCore.PublishToNode(MyQItem.DirectTarget, TheBaseAssets.MyScopeManager.GetScrambledScopeID(MyQItem.RealScopeID, true), SendToClients);
                            string tPublish = MyQItem.DataStoreGramm.UID;
                            if (!string.IsNullOrEmpty(MyQItem.SScopeID))
                                tPublish += "@" + MyQItem.SScopeID;
                            //tPublish += "@" + TheBaseAssets.MyScopeManager.GetScrambledScopeID(MyQItem.RealScopeID, true);
                            TheCommCore.PublishCentral(tPublish, SendToClients);
                        }
                        MyQItem.LocalCallback?.Invoke(SendToClients);
                    }
                    catch (Exception e)
                    {
                        AppendError("SQL Update/Insert error", e.ToString());
                    }
                    if(doRemoveItem)
                        MyInsertQueue.RemoveAnItemByID(MyQItemKey, null);
                }
            }
        }



        Int32 ConnectionRetryWaitSeconds(Int32 attempt)
        {
            // Backoff Throttling
            Int32 connectionRetryWaitSeconds = (mRetrySeconds*100) *
                (Int32)Math.Pow(2, attempt);
            return (connectionRetryWaitSeconds);
        }

        /// <summary>
        /// Determine from the exception if the execution
        /// of the connection should Be attempted again
        /// </summary>
        /// <param name="sqlException">Generic Exception</param>
        /// <returns>True if a a retry is needed, false if not</returns>
        static Boolean RetryLitmus(SqlException sqlException)
        {
            switch (sqlException.Number)
            {
                // The service has encountered an error
                // processing your request. Please try again.
                // Error code %d.
                case 40197:
                // The service is currently busy. Retry
                // the request after 10 seconds. Code: %d.
                case 40501:
                //A transport-level error has occurred when
                // receiving results from the server. (provider:
                // TCP Provider, error: 0 - An established connection
                // was aborted by the software in your host machine.)
                case 10053:
                // A network-related or instance-specific error occurred 
                // while establishing a connection to SQL Server. The server 
                // was not found or was not accessible. Verify that the instance 
                // name is correct and that SQL Server is configured to allow remote connections.
                case 2:
                    return (true);
            }

            return (false);
        }
	}
}
