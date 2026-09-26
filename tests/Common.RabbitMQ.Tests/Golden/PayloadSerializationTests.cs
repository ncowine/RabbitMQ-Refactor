using System;
using System.Collections.Generic;
using Common.Events;
using Xunit;

namespace Common.RabbitMQ.Tests.Golden
{
    /// <summary>
    /// Message bodies: UTF-8 JSON as Newtonsoft 12 writes it with default settings (ADR 0001, sections 3 and 4).
    /// Each case checks both directions: the payload writes the golden bytes, and the golden bytes read back to the payload.
    /// </summary>
    public class PayloadSerializationTests
    {
        [Fact]
        public void Employee()
        {
            Employee employee = new Employee
            {
                Id = 7,
                Name = "Ada Lovelace",
                Department = "Engineering",
                UpdatedBy = "WpfApp.Net472",
                UpdatedAt = new DateTime(2026, 9, 26, 10, 15, 30, 123, DateTimeKind.Unspecified),
            };

            AssertGolden("employee.json", employee);

            Employee read = GoldenFile.FromLegacyBody<Employee>(GoldenFile.Read("employee.json"));
            AssertEmployee(employee, read);
        }

        [Fact]
        public void Employee_UtcDateAndNulls()
        {
            Employee employee = new Employee
            {
                Id = 0,
                Name = null,
                Department = null,
                UpdatedBy = "",
                UpdatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            };

            AssertGolden("employee-utc-nulls.json", employee);

            Employee read = GoldenFile.FromLegacyBody<Employee>(GoldenFile.Read("employee-utc-nulls.json"));
            AssertEmployee(employee, read);
        }

        [Fact]
        public void Employee_Escaping()
        {
            // Newtonsoft escapes quotes, backslashes and control characters only; non-ASCII and <&> are written as is.
            Employee employee = new Employee
            {
                Id = 1,
                Name = "Zoë \"Z\" <&> \\ é\n\t",
                Department = "R&D",
                UpdatedBy = "a/b",
                UpdatedAt = new DateTime(2026, 9, 26),
            };

            AssertGolden("employee-escaping.json", employee);

            Employee read = GoldenFile.FromLegacyBody<Employee>(GoldenFile.Read("employee-escaping.json"));
            AssertEmployee(employee, read);
        }

        [Fact]
        public void TypeCoverage()
        {
            GoldenPayload payload = new GoldenPayload
            {
                Amount = 12.50m,
                WholeAmount = 12m,
                Ratio = 1.5,
                WholeRatio = 2d,
                Color = GoldenColor.Blue,
                Key = new Guid("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
                At = new DateTimeOffset(2026, 9, 26, 10, 15, 30, TimeSpan.FromHours(2)),
                Missing = null,
                Tags = new List<string> { "a", "b" },
                Data = new byte[] { 1, 2, 3 },
                Flag = true,
                Big = 9007199254740993L,
            };

            AssertGolden("type-coverage.json", payload);

            GoldenPayload read = GoldenFile.FromLegacyBody<GoldenPayload>(GoldenFile.Read("type-coverage.json"));
            Assert.Equal(payload.Amount, read.Amount);
            Assert.Equal(payload.WholeAmount, read.WholeAmount);
            Assert.Equal(payload.Ratio, read.Ratio);
            Assert.Equal(payload.WholeRatio, read.WholeRatio);
            Assert.Equal(payload.Color, read.Color);
            Assert.Equal(payload.Key, read.Key);
            Assert.Equal(payload.At, read.At);
            Assert.Equal(payload.At.Offset, read.At.Offset);
            Assert.Null(read.Missing);
            Assert.Equal(payload.Tags, read.Tags);
            Assert.Equal(payload.Data, read.Data);
            Assert.Equal(payload.Flag, read.Flag);
            Assert.Equal(payload.Big, read.Big);
        }

        [Fact]
        public void CompatPayload()
        {
            // The broker tests compare what the service actually sends against this file.
            AssertGolden("compat-payload.json", CompatPayloads.Golden());
        }

        private static void AssertGolden(string file, object payload)
        {
            Assert.Equal(GoldenFile.Read(file), GoldenFile.LegacyBody(payload));
        }

        private static void AssertEmployee(Employee expected, Employee actual)
        {
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Department, actual.Department);
            Assert.Equal(expected.UpdatedBy, actual.UpdatedBy);
            Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
            Assert.Equal(expected.UpdatedAt.Kind, actual.UpdatedAt.Kind);
        }
    }
}
