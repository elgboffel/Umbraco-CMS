﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NPoco;
using NUnit.Framework;
using Semver;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Profiling;
using Umbraco.Core.Services;
using Umbraco.Web.Strategies.Migrations;

namespace Umbraco.Tests.Persistence.Migrations
{
    [TestFixture]
    public class MigrationStartupHandlerTests
    {
        // NPoco wants a DbConnection and NOT an IDbConnection
        // and DbConnection is hard to mock...
        private class MockConnection : DbConnection
        {
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                return Mock.Of<DbTransaction>(); // enough here
            }

            public override void Close()
            {
                throw new NotImplementedException();
            }

            public override void ChangeDatabase(string databaseName)
            {
                throw new NotImplementedException();
            }

            public override void Open()
            {
                throw new NotImplementedException();
            }

            public override string ConnectionString { get; set; }

            protected override DbCommand CreateDbCommand()
            {
                throw new NotImplementedException();
            }

            public override string Database { get; }
            public override string DataSource { get; }
            public override string ServerVersion { get; }
            public override ConnectionState State => ConnectionState.Open; // else NPoco reopens
        }

        [Test]
        public void Executes_For_Any_Product_Name_When_Not_Specified()
        {
            var changed1 = new Args { CountExecuted = 0 };
            var testHandler1 = new TestMigrationHandler(changed1);
            testHandler1.OnApplicationStarting(Mock.Of<UmbracoApplicationBase>(), new ApplicationContext(CacheHelper.CreateDisabledCacheHelper(), new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>())));
            
            var conn = new MockConnection();
            var db = new Mock<Database>(conn);

            var runner1 = new MigrationRunner(Mock.Of<IMigrationResolver>(), Mock.Of<IMigrationEntryService>(), Mock.Of<ILogger>(), new SemVersion(1), new SemVersion(2), "Test1",
                new IMigration[] { Mock.Of<IMigration>() });
            var result1 = runner1.Execute(db.Object, DatabaseProviders.SqlServerCE, new SqlCeSyntaxProvider(), (false));
            Assert.AreEqual(1, changed1.CountExecuted);            
        }

        [Test]
        public void Executes_Only_For_Specified_Product_Name()
        {
            var changed1 = new Args { CountExecuted = 0};
            var testHandler1 = new TestMigrationHandler("Test1", changed1);
            testHandler1.OnApplicationStarting(Mock.Of<UmbracoApplicationBase>(), new ApplicationContext(CacheHelper.CreateDisabledCacheHelper(), new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>())));
            var changed2 = new Args { CountExecuted = 0 };
            var testHandler2 = new TestMigrationHandler("Test2", changed2);
            testHandler2.OnApplicationStarting(Mock.Of<UmbracoApplicationBase>(), new ApplicationContext(CacheHelper.CreateDisabledCacheHelper(), new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>())));

            var conn = new MockConnection();
            var db = new Mock<Database>(conn);

            var runner1 = new MigrationRunner(Mock.Of<IMigrationResolver>(), Mock.Of<IMigrationEntryService>(), Mock.Of<ILogger>(), new SemVersion(1), new SemVersion(2), "Test1",
                new IMigration[] { Mock.Of<IMigration>()});
            var result1 = runner1.Execute(db.Object, DatabaseProviders.SqlServerCE, new SqlCeSyntaxProvider(), false);
            Assert.AreEqual(1, changed1.CountExecuted);
            Assert.AreEqual(0, changed2.CountExecuted);

            var runner2 = new MigrationRunner(Mock.Of<IMigrationResolver>(), Mock.Of<IMigrationEntryService>(), Mock.Of<ILogger>(), new SemVersion(1), new SemVersion(2), "Test2",
                new IMigration[] { Mock.Of<IMigration>() });            
            var result2 = runner2.Execute(db.Object, DatabaseProviders.SqlServerCE, new SqlCeSyntaxProvider(), false);
            Assert.AreEqual(1, changed1.CountExecuted);
            Assert.AreEqual(1, changed2.CountExecuted);
        }

        public class Args
        {
            public int CountExecuted { get; set; }
        }

        public class TestMigrationHandler : MigrationStartupHandler
        {
            private readonly string _prodName;
            private readonly Args _changed;

            // need that one else it breaks IoC
            public TestMigrationHandler()
            {
                _changed = new Args();
            }

            public TestMigrationHandler(Args changed)
            {
                _changed = changed;
            }

            public TestMigrationHandler(string prodName, Args changed)
            {
                _prodName = prodName;
                _changed = changed;
            }

            protected override void AfterMigration(MigrationRunner sender, MigrationEventArgs e)
            {
                _changed.CountExecuted++;
            }
            
            public override string[] TargetProductNames
            {
                get { return _prodName.IsNullOrWhiteSpace() ? new string[] {} : new[] {_prodName}; }
            }
        }
    }
}
