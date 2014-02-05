using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;

namespace Lokad.SqlToTsv
{
    public class Program
    {
        static ConsoleLog _log;

        static void Main(string[] args)
        {
            _log = new ConsoleLog();
            _log.Info("Utility for uploading SQL data to FTP v" + Assembly.GetExecutingAssembly().GetName().Version);

            if (args.Length == 1 && args[0] == "--help")
            {
                _log.Info(@"Usage
Lokad.sqltotsv.exe <SQL Host> <Database> <Login> <Password> <FTP login> <FTP password>

Where
    <SQL Host> is the host name of the SQL Server
    <Database> is the database name
    <Login> is the username to log into the database
    <Password> is password to log into the database
    <FTP login> should be the email address used when registering on Lokad.
    <FTP password> should be the password used when registering on Lokad.");
                return;
            }

            var sqlHost = args.Length > 0 ? args[0] : ConfigurationManager.AppSettings["sqlHost"];
            var sqlDb = args.Length > 1 ? args[1] : ConfigurationManager.AppSettings["sqlDb"];
            var sqlLogin = args.Length > 2 ? args[2] : ConfigurationManager.AppSettings["sqlLogin"];
            var sqlPass = args.Length > 3 ? args[3] : ConfigurationManager.AppSettings["sqlPass"];

            const string ftpHost = "files.lokad.com"; // hard-coding
            const string ftpFolder = ""; // HACK: hard-coding the root folder
            var ftpLogin = args.Length > 4 ? args[4] : ConfigurationManager.AppSettings["ftpLogin"];
            var ftpPass = args.Length > 5 ? args[5] : ConfigurationManager.AppSettings["ftpPass"];

            var connectionString = string.Format("Server={0};Database={1};User Id={2};Password={3};", sqlHost, sqlDb,
                sqlLogin, sqlPass);

            using (var connection = new SqlConnection(connectionString))
            {
                _log.Info("Open connection to SQL server ");
                connection.Open();

                _log.Info("Call BeforeSalescastForecast stored procedure...");
                try
                {
                    using (var cmd = new SqlCommand("CALL `BeforeSalescastForecast`"))
                    {
                        cmd.ExecuteNonQuery();

                        _log.Info("Stored procedure finished with success.");
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn("Run BeforeSalescastForecast failed. ", ex.Message);
                }

                _log.Info("Exporting Lokad_Items...");

                var tempFileItems = Path.GetTempFileName();
                var count = LoadItems(connection, "SELECT * FROM [Lokad_Items] ORDER BY [Id]", tempFileItems);
                _log.Success("Loaded {0} items", count);

                _log.Info("Exporting Lokad_Orders...");

                var tempFileOrders = Path.GetTempFileName();
                count = LoadItems(connection, "SELECT * FROM [Lokad_Orders] ORDER BY [Id]", tempFileOrders);
                _log.Success("Loaded {0} orders", count);

                _log.Info("Upload Lokad_Items.tsv file...", ftpHost);
                UploadFile(ftpLogin, ftpPass, ftpHost, ftpFolder, tempFileItems, "Lokad_Items.tsv");
                _log.Success("Lokad_Items.tsv files uploaded");

                _log.Info("Upload Lokad_Orders.tsv file...", ftpHost);
                UploadFile(ftpLogin, ftpPass, ftpHost, ftpFolder, tempFileOrders, "Lokad_Orders.tsv");
                _log.Success("Lokad_Orders.tsv files uploaded");

                _log.Success("Export SQL data to FTP executed successefully");
            }
        }

        static int LoadItems(SqlConnection connection, string command, string file)
        {
            var count = 0;
            using (var cmd = new SqlCommand(command, connection))
            using (var reader = cmd.ExecuteReader())
            using (var wr = new StreamWriter(file))
            {
                var header = "";
                var isHeaderEmpty = true;
                while (reader.Read())
                {
                    var line = string.Empty;
                    count++;

                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        if (isHeaderEmpty)
                            if (i < reader.FieldCount - 1)
                            {
                                header += CleanTsv(reader.GetName(i)) + "\t";
                            }
                            else
                            {
                                header += CleanTsv(reader.GetName(i));
                            }

                        var dataType = reader[i].GetType();
                        string value;
                        if (dataType == typeof(DateTime))
                        {
                            value = ((DateTime) reader[i]).ToString("yyyy-MM-dd");
                        }
                        else if (dataType == typeof(Double))
                        {
                            value = ((Double) reader[i]).ToString("0.##", CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            value = CleanTsv(reader[i].ToString());
                        }

                        if (i < reader.FieldCount - 1)
                        {
                            line += value + "\t";
                        }
                        else
                        {
                            line += value;
                        }
                    }
                    
                    if (isHeaderEmpty)
                    {
                        wr.WriteLine(header);
                        isHeaderEmpty = false;
                    }

                    wr.WriteLine(line);
                }
            }
            return count;
        }

        static void UploadFile(string ftpLogin, string ftpPass, string ftpHost, string ftpFolder, string tempFileItems, string fileName)
        {
            var req =
                (FtpWebRequest)
                    WebRequest.Create(new Uri(string.Format("ftp://{0}/{1}/{2}", ftpHost, ftpFolder, fileName)));

            req.Method = WebRequestMethods.Ftp.UploadFile;
            req.UseBinary = true;
            req.KeepAlive = true;
            req.Credentials = new NetworkCredential(ftpLogin, ftpPass);

            using (var reader = new FileStream(tempFileItems, FileMode.Open, FileAccess.Read))
            using (var stream = req.GetRequestStream())
            {
                var buffer = new byte[reader.Length];
                reader.Read(buffer, 0, (int)reader.Length);
                reader.Close();

                stream.Write(buffer, 0, buffer.Length);
            }

            req.GetResponse();
        }

        public static string CleanTsv(string value)
        {
            if (_log != null)
            {
                if (value.Contains("\t"))
                {
                    _log.Warn("Value \"{0}\" contains tabs. These will replaced with space.", value);
                }

                if (value.Contains("\r") || value.Contains("\n"))
                {
                    _log.Warn("Value \"{0}\" contains newlines. These will replaced with space.", value);
                }
            }

            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }

    public class ConsoleLog
    {
        readonly ConsoleColor _defaultColor;

        public ConsoleLog()
        {
            _defaultColor = Console.ForegroundColor;
        }

        public void Info(string message, params object[] args)
        {
            Console.ForegroundColor = _defaultColor;

            Log(string.Format(message, args));
        }

        public void Success(string message, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(string.Format(message, args));
        }

        public void Warn(string message, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(string.Format(message, args));
        }
        public void Error(string message, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(string.Format(message, args));
        }

        private void Log(string message)
        {
            Console.WriteLine("[{0:dd-MM-yyyy HH:mm}] {1}", DateTime.Now, message);
            Console.ForegroundColor = _defaultColor;
        }
    }
}
