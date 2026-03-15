using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

namespace NuExt.System.Data.SQLite.Tests;

public class Tests
{
    private static SQLiteDbConnection CreateInMemoryConnection()
    {
        var csb = new SQLiteConnectionStringBuilder
        {
            DataSource = ":memory:"
        };
        return new SQLiteDbConnection(csb.ToString(), true);
    }

    private static void CreateSampleDataBase(SQLiteDbConnection conn)
    {
        conn.ExecuteInTransaction(trans =>
        {
            int result = conn.ExecuteNonQuery(@"
CREATE TABLE Sample (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT,
	Int32Value INTEGER NOT NULL DEFAULT 0,
    Int32NullableValue INTEGER,
    BoolValue INTEGER NOT NULL DEFAULT 1);");
            Debug.Assert(result == 0);
            trans.Commit();
        });
    }

    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestIsDisposed()
    {
        var conn = CreateInMemoryConnection();
        using (conn.SuspendAsserts())
        {
            conn.Dispose();
            Assert.That(conn.IsDisposed, Is.True);
        }
    }

    [Test]
    public void TestIsOpen()
    {
        using var conn = CreateInMemoryConnection();
        using(conn.SuspendAsserts())
        {
            conn.Open();
            Assert.That(conn.IsOpen, Is.True);
            conn.Close();
        }
    }

    [Test]
    public void TestInMemory()
    {
        using var conn = CreateInMemoryConnection();
        using (conn.SuspendAsserts())
        {
            conn.Open();
            CreateSampleDataBase(conn);
            Assert.That(conn.IsOpen, Is.True);

            var (adapter, table) = conn.SelectForUpdate("SELECT * FROM Sample", true);
            Assert.That(table.PrimaryKey, Has.Length.EqualTo(0));
            using (adapter)
            {
                for (int i = 0; i < 10; i++)
                {
                    var row = table.NewRow();
                    row["Name"] = $"Name{i}";
                    row["Int32Value"] = i;
                    table.Rows.Add(row);
                }
                int num = conn.Update(adapter, table);
                Assert.That(num, Is.EqualTo(10));
            }
            table.ClearAndDispose();

            var numRead1 = conn.ExecuteReader("SELECT * FROM Sample", null);
            Assert.That(numRead1, Is.EqualTo(10));

            (adapter, table) = conn.SelectForUpdate("SELECT * FROM Sample", true);
            Assert.That(table.Rows, Has.Count.EqualTo(10));
            //table.PrimaryKey = new [] { table.Columns["ID"] };
            using (adapter)
            {
                int expected = 0;
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    if (i % 2 == 0)
                    {
                        row["Description"] = $"Description{i}";
                        row["Int32NullableValue"] = i;
                        row["BoolValue"] = 0;
                        expected++;
                    }
                    else if (i % 3 == 0)
                    {
                        row.Delete();
                        expected++;
                    }
                }
                int num = conn.Update(adapter, table);
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(expected, Is.EqualTo(7));
                    Assert.That(num, Is.EqualTo(expected));
                }
            }
            table.ClearAndDispose();

            var listReader =
                new List<(string Name, string? Description, int Int32Value, int? Int32NullableValue, bool BoolValue
                    )>();
            var numRead2 = conn.ExecuteReader("SELECT * FROM Sample", (reader) =>
            {
                listReader.Add((
                    reader.GetString("Name"),
                    reader.GetNullableString("Description"),
                    reader.GetInt32("Int32Value"),
                    reader.GetNullableInt32("Int32NullableValue"),
                    reader.GetBoolean("BoolValue")
                ));
            });
            Assert.That(numRead2, Is.EqualTo(listReader.Count));

            table = conn.Select("SELECT * FROM Sample");
            Assert.That(table.Rows, Has.Count.EqualTo(8));
            var listTable =
                new List<(string Name, string? Description, int Int32Value, int? Int32NullableValue, bool BoolValue
                    )>();
            foreach (DataRow row in table.Rows)
            {
                listTable.Add((
                    row.GetString("Name"),
                    row.GetNullableString("Description"),
                    row.GetInt32("Int32Value"),
                    row.GetNullableInt32("Int32NullableValue"),
                    row.GetBoolean("BoolValue")
                ));
            }
            Assert.That(listTable, Has.Count.EqualTo(listReader.Count));

            Assert.That(listTable.SequenceEqual(listReader), Is.True);

            var dr = table.FindRow("Description", o => "Description2".Equals(o));
            Assert.That(dr, Is.Not.Null);

            Assert.Pass();
        }
    }
}