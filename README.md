# lokad-sql-to-tsv

Stand-alone console utility to reformat Salescast SQL data in the new Salescast TSV format.

## Context

As of July 3rd, 2013, the SQL connector is being [phased out from Salescast](http://blog.lokad.com/journal/2013/7/3/phasing-out-sql-from-salescast). The present application helps the Lokad customers to transition from their old SQL data to the new TSV format.

## Features

The app perform the following sequence of operations:

1. Extraction of the SQL data (following the original SQL format expected by Salescast).
2. Reformating of the data into the new TSV format of Salescast.
3. Upload of the files to [BigFiles](http://www.lokad.com/ftp-hosting), the file hosting service of Lokad.

## Command line arguments

The excutable must be launched from the Windows command line with the following arguments:

    Lokad.sqltotsv.exe <SQL Host> <Database> <Login> <Password> <FTP login> <FTP password>
    
Where
* <SQL Host> is the host name of the SQL Server
* <Database> is the database name
* <Login> is the username to log into the database
* <Password> is password to log into the database
* <FTP login> should be the email address used when registering on Lokad.
* <FTP password> should be the password used when registering on Lokad.
