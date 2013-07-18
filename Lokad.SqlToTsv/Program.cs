using System;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;

namespace Lokad.SqlToTsv
{
    class Program
    {
        static ConsoleLog _log;

        static void Main(string[] args)
        {
            _log = new ConsoleLog();
            _log.Info("Utility for uploading SQL data to FTP v" + Assembly.GetExecutingAssembly().GetName().Version);

            if (args.Length < 8)
            {
                _log.Error("Not all arguments passed.");
                _log.Info(@"Usage
Lokad.sqltotsv.exe <SQL Host> <Database> <Login> <Password> <FTP host> <FTP folder> <FTP login> <FTP password>
    <FTP folder> must exist before uploading files");
                return;
            }

            var sqlHost = args[0];
            var sqlDb = args[1];
            var sqlLogin = args[2];
            var sqlPass = args[3];

            var ftpHost = args[4];
            var ftpFolder = args[5];
            var ftpLogin = args[6];
            var ftpPass = args[7];

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
                                header += CleanTabs(reader.GetName(i)) + "\t";
                            }
                            else
                            {
                                header += CleanTabs(reader.GetName(i));
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
                            value = CleanTabs(reader[i].ToString());
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

        private static string CleanTabs(string value)
        {
            if (value.Contains("\t"))
            {
                _log.Warn("Value \"{0}\" contain tab. Tabs will replaced with space.", value);
            }
            return value.Replace("\t", " ");
        }
    }

    public class ConsoleLog
    {
        ConsoleColor _defaultColor;

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
