using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using System.Data;

namespace SematiNotificationAutoFix.Console.Extensions;

public static class LoggingExtensions
{
    public static IHostApplicationBuilder AddSerilogLogging(this IHostApplicationBuilder builder)
    {


        var config = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext();

        // -- db log confgi --
        // var columnOptions = new ColumnOptions();
        // // columnOptions.Store.Clear();
        // columnOptions.AdditionalColumns =
        // [
        //     new SqlColumn { ColumnName = "ActionId",    DataType = SqlDbType.Int,      AllowNull = true },
        //     new SqlColumn { ColumnName = "Tcn",         DataType = SqlDbType.NVarChar, DataLength = 100, AllowNull = true },
        //     new SqlColumn { ColumnName = "PersonId",    DataType = SqlDbType.NVarChar, DataLength = 50,  AllowNull = true },
        //     new SqlColumn { ColumnName = "ProcessName", DataType = SqlDbType.NVarChar, DataLength = 50,  AllowNull = true },
        // ];

        //config.WriteTo.MSSqlServer(
        //    connectionString: builder.Configuration.GetConnectionString("Logs"),
        //    sinkOptions: new MSSqlServerSinkOptions
        //    {
        //        TableName = "Logs",
        //        AutoCreateSqlTable = false
        //    },
        //    columnOptions: columnOptions);

        Log.Logger = config.CreateLogger();

        builder.Logging.AddSerilog();

        return builder;
    }
}
