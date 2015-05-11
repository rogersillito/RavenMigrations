﻿using System.Linq;
using FluentAssertions;
using Raven.Abstractions.Data;
using Raven.Client.Indexes;
using Raven.Tests.Helpers;
using Xunit;

namespace RavenMigrations.Tests
{
    public class RunnerTests : RavenTestBase
    {
        [Fact]
        public void Document_id_prefix_is_ravenmigrations()
        {
            RavenMigrationHelpers.RavenMigrationsIdPrefix.Should().Be("ravenmigrations");
        }

        [Fact]
        public void Can_change_migration_document_seperator_to_dash()
        {
            new _First___Migration().GetMigrationIdFromName(seperator: '-')
                .Should().Be("ravenmigrations-first-migration-1");
        }

        [Fact]
        public void Document_id_prefix_is_raven_migrations()
        {
            RavenMigrationHelpers.RavenMigrationsIdPrefix.Should().Be("ravenmigrations");
        }

        [Fact]
        public void Can_get_migration_id_from_migration()
        {
            var id = new _First___Migration().GetMigrationIdFromName();
            id.Should().Be("ravenmigrations/first/migration/1");
        }

        [Fact]
        public void Can_get_migration_id_from_migration_and_correct_leading_or_multiple_underscores()
        {
            var id = new _has_problems__with_underscores___().GetMigrationIdFromName();
            id.Should().Be("ravenmigrations/has/problems/with/underscores/5");
        }

        [Fact]
        public void Can_get_migration_attribute_from_migration_type()
        {
            var attribute = typeof(_First___Migration).GetMigrationAttribute();
            attribute.Should().NotBeNull();
            attribute.Version.Should().Be(1);
        }

        [Fact]
        public void Default_migration_direction_is_up()
        {
            var options = new MigrationOptions();
            options.Direction.Should().Be(Directions.Up);
        }

        [Fact]
        public void Default_resolver_should_be_DefaultMigrationResolver()
        {
            var options = new MigrationOptions();
            options.MigrationResolver.Should().NotBeNull();
            options.MigrationResolver.Should().BeOfType<DefaultMigrationResolver>();
        }

        [Fact]
        public void Default_migration_resolver_can_instantiate_a_migration()
        {
            var migration = new DefaultMigrationResolver().Resolve(typeof(_First___Migration));
            migration.Should().NotBeNull();
        }

        [Fact]
        public void Can_run_an_up_migration_against_a_document_store()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store);
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    session.Query<TestDocument, TestDocumentIndex>()
                        .Count()
                        .Should().Be(1);
                }
            }
        }

        [Fact]
        public void Calling_run_twice_runs_migrations_only_once()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store);
                // oooops, twice!
                Runner.Run(store);
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    session.Query<TestDocument, TestDocumentIndex>()
                        .Count()
                        .Should().Be(1);
                }
            }
        }

        [Fact]
        public void Can_call_up_then_down_on_migrations()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store);

                WaitForIndexing(store);

                // flip it and reverse it :P
                Runner.Run(store, new MigrationOptions
                {
                    Direction = Directions.Down
                });

                WaitForIndexing(store);
                using (var session = store.OpenSession())
                {
                    session.Query<TestDocument, TestDocumentIndex>()
                        .Count()
                        .Should().Be(0);
                }
            }
        }

        [Fact]
        public void Can_call_migrations_up_to_a_certain_version()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store, new MigrationOptions() { ToVersion = 1 });
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    session.Query<TestDocument, TestDocumentIndex>()
                        .Count()
                        .Should().Be(1);

                    var shouldNotExist = session.Load<object>("second-document");
                    shouldNotExist.Should().BeNull();
                }
            }
        }

        [Fact]
        public void Can_call_migrations_down_to_a_certain_version()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store);
                WaitForIndexing(store);

                // migrate down to 
                Runner.Run(store, new MigrationOptions
                {
                    Direction = Directions.Down,
                    ToVersion = 2
                });

                using (var session = store.OpenSession())
                {
                    session.Query<TestDocument, TestDocumentIndex>()
                        .Count()
                        .Should().Be(1);

                    var secondMigrationDocument =
                        session.Load<MigrationDocument>(new Second_Migration().GetMigrationIdFromName());
                    secondMigrationDocument.Should().BeNull();

                    var firstMigrationDocument =
                        session.Load<MigrationDocument>(new _First___Migration().GetMigrationIdFromName());
                    firstMigrationDocument.Should().NotBeNull();
                }
            }
        }

        [Fact]
        public void Can_call_migrations_with_profile()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store, new MigrationOptions { Profiles = new[] { "development" } });
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var development = session.Load<object>("development-1");
                    development.Should().NotBeNull();
                }
            }
        }

        [Fact]
        public void Can_call_migrations_ignore_migrations_with_profile()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store);
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var development = session.Load<object>("development-1");
                    development.Should().BeNull();
                }
            }
        }

        [Fact]
        public void Can_call_migrations_that_are_not_direct_subclasses_of_Migration()
        {
            using (var store = NewDocumentStore())
            {
                new TestDocumentIndex().Execute(store);

                Runner.Run(store, new MigrationOptions {Profiles = new[] {"uses-BaseMigration"}});
                WaitForIndexing(store);

                using (var session = store.OpenSession())
                {
                    var development = session.Load<object>("migrated-using-BaseMigration");
                    development.Should().NotBeNull();
                }
            }
        }
    }

    public class TestDocument
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class TestDocumentIndex : AbstractIndexCreationTask<TestDocument>
    {
        public TestDocumentIndex()
        {
            Map = tests => from t in tests
                           select new { t.Id, t.Name };
        }
    }

    [Migration(1)]
    public class _First___Migration : Migration
    {
        public override void Up()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(new TestDocument { Name = "Khalid Abuhakmeh" });
                session.SaveChanges();
            }
        }

        public override void Down()
        {
            WaitForIndexing();
            DocumentStore.DatabaseCommands.DeleteByIndex(new TestDocumentIndex().IndexName, new IndexQuery());
        }
    }

    [Migration(2)]
    public class Second_Migration : Migration
    {
        public override void Up()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(new { Id = "second-document", Name = "woot!" });
                session.SaveChanges();
            }
        }

        public override void Down()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var doc = session.Load<object>("second-document");
                session.Delete(doc);
                session.SaveChanges();
            }
        }
    }

    [Migration(3, "development")]
    public class Development_Migration : Migration
    {
        public override void Up()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(new { Id = "development-1" });
                session.SaveChanges();
            }
        }
    }

    [Migration(4, "uses-BaseMigration")]
    public class Subclass_of_BaseMigration : BaseMigration
    {
        public override void Up()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(new { Id = "migrated-using-BaseMigration" });
                session.SaveChanges();
            }
        }
    }    

    [Migration(5, "exclude-me")]
    public class _has_problems__with_underscores___ : Migration
    {
        public override void Up()
        {
        }
    }    

    public class BaseMigration : Migration
    {
        public override void Up()
        {
        }
    }
}
