using System.Diagnostics;

namespace System.Data.SQLite.Tests;


[TestClass]
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
            int result = conn.ExecuteNonQuery("""
                                              CREATE TABLE Sample (
                                                  Id INTEGER PRIMARY KEY,
                                                  Name TEXT NOT NULL,
                                                  Description TEXT,
                                              	Int32Value INTEGER NOT NULL DEFAULT 0,
                                                  Int32NullableValue INTEGER,
                                                  BoolValue INTEGER NOT NULL DEFAULT 1);
                                              """);
            Debug.Assert(result == 0);
            trans.Commit();
        });
    }


    [TestMethod]
    public void TestIsDisposed()
    {
        var conn = CreateInMemoryConnection();
        using (conn.SuspendAsserts())
        {
            conn.Dispose();
            Assert.IsTrue(conn.IsDisposed);
        }
    }

    [TestMethod]
    public void TestIsOpen()
    {
        using var conn = CreateInMemoryConnection();
        using(conn.SuspendAsserts())
        {
            conn.Open();
            Assert.IsTrue(conn.IsOpen);
            conn.Close();
        }
    }

    [TestMethod]
    public void TestInMemory()
    {
        using var conn = CreateInMemoryConnection();
        using (conn.SuspendAsserts())
        {
            conn.Open();
            CreateSampleDataBase(conn);
            Assert.IsTrue(conn.IsOpen);

            var (adapter, table) = conn.SelectForUpdate("SELECT * FROM Sample", true, CancellationToken.None);
            Assert.IsEmpty(table.PrimaryKey);
            using (adapter)
            {
                for (int i = 0; i < 10; i++)
                {
                    var row = table.NewRow();
                    row["Name"] = $"Name{i}";
                    row["Int32Value"] = i;
                    table.Rows.Add(row);
                }
                int num = conn.Update(adapter, table, CancellationToken.None);
                Assert.AreEqual(10, num);
            }
            table.ClearAndDispose();

            var numRead1 = conn.ExecuteReader("SELECT * FROM Sample", null, CommandBehavior.Default, CancellationToken.None);
            Assert.AreEqual(10, numRead1);

            (adapter, table) = conn.SelectForUpdate("SELECT * FROM Sample", true, CancellationToken.None);
            Assert.HasCount(10, table.Rows);
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
                int num = conn.Update(adapter, table, CancellationToken.None);
                Assert.AreEqual(7, expected);
                Assert.AreEqual(expected, num);
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
            }, CommandBehavior.Default, CancellationToken.None);
            Assert.AreEqual(listReader.Count, numRead2);

            table = conn.Select("SELECT * FROM Sample", CancellationToken.None);
            Assert.HasCount(8, table.Rows);
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
            Assert.HasCount(listReader.Count, listTable);

            Assert.IsTrue(listTable.SequenceEqual(listReader));

            var dr = table.FindRow("Description", o => "Description2".Equals(o));
            Assert.IsNotNull(dr);
        }
    }
}