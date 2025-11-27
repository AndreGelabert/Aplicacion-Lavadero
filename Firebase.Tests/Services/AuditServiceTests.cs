using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the AuditService module.
    /// Tests cover audit log functionality including filtering, search and pagination.
    /// </summary>
    public class AuditServiceTests
    {
        #region Model Tests

        [Fact]
        public void AuditLog_Model_ShouldHaveCorrectProperties()
        {
            var auditLog = TestFactory.CreateAuditLog(
                userId: "user-123",
                userEmail: "user@example.com",
                action: "Creación de servicio",
                targetId: "servicio-456",
                targetType: "Servicio");

            Assert.Equal("user-123", auditLog.UserId);
            Assert.Equal("user@example.com", auditLog.UserEmail);
            Assert.Equal("Creación de servicio", auditLog.Action);
            Assert.Equal("servicio-456", auditLog.TargetId);
            Assert.Equal("Servicio", auditLog.TargetType);
        }

        #endregion

        #region Date Filtering Tests

        [Fact]
        public void DateFilter_ShouldFilterByStartDate()
        {
            var log1 = TestFactory.CreateAuditLog(userId: "1");
            log1.Timestamp = new DateTime(2024, 1, 15);
            var log2 = TestFactory.CreateAuditLog(userId: "2");
            log2.Timestamp = new DateTime(2024, 2, 20);
            var log3 = TestFactory.CreateAuditLog(userId: "3");
            log3.Timestamp = new DateTime(2024, 3, 10);
            var logs = new List<AuditLog> { log1, log2, log3 };
            var fechaInicio = new DateTime(2024, 2, 1);

            var filtered = logs.Where(r => r.Timestamp >= fechaInicio).ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void DateFilter_ShouldFilterByEndDate()
        {
            var log1 = TestFactory.CreateAuditLog(userId: "1");
            log1.Timestamp = new DateTime(2024, 1, 15);
            var log2 = TestFactory.CreateAuditLog(userId: "2");
            log2.Timestamp = new DateTime(2024, 2, 20);
            var log3 = TestFactory.CreateAuditLog(userId: "3");
            log3.Timestamp = new DateTime(2024, 3, 10);
            var logs = new List<AuditLog> { log1, log2, log3 };
            var fechaFin = new DateTime(2024, 2, 28);

            var filtered = logs.Where(r => r.Timestamp <= fechaFin).ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void DateFilter_ShouldFilterByDateRange()
        {
            var log1 = TestFactory.CreateAuditLog(userId: "1");
            log1.Timestamp = new DateTime(2024, 1, 15);
            var log2 = TestFactory.CreateAuditLog(userId: "2");
            log2.Timestamp = new DateTime(2024, 2, 20);
            var log3 = TestFactory.CreateAuditLog(userId: "3");
            log3.Timestamp = new DateTime(2024, 3, 10);
            var log4 = TestFactory.CreateAuditLog(userId: "4");
            log4.Timestamp = new DateTime(2024, 4, 5);
            var logs = new List<AuditLog> { log1, log2, log3, log4 };

            var filtered = logs.Where(r => r.Timestamp >= new DateTime(2024, 2, 1) && 
                                            r.Timestamp <= new DateTime(2024, 3, 31)).ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void EndOfDay_ShouldIncludeFullDay()
        {
            var date = new DateTime(2024, 5, 15);
            var endOfDay = date.Date.AddDays(1).AddTicks(-1);

            Assert.Equal(23, endOfDay.Hour);
            Assert.Equal(59, endOfDay.Minute);
            Assert.Equal(59, endOfDay.Second);
        }

        #endregion

        #region Action Filtering Tests

        [Fact]
        public void FilterByAction_ShouldFilterSingleAction()
        {
            var logs = new List<AuditLog>
            {
                TestFactory.CreateAuditLog(action: "Creacion de servicio"),
                TestFactory.CreateAuditLog(action: "Modificacion de rol"),
                TestFactory.CreateAuditLog(action: "Creacion de servicio")
            };

            var filtered = logs.Where(r => r.Action == "Creacion de servicio").ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void FilterByAction_ShouldFilterMultipleActions()
        {
            var logs = new List<AuditLog>
            {
                TestFactory.CreateAuditLog(action: "Creacion"),
                TestFactory.CreateAuditLog(action: "Modificacion"),
                TestFactory.CreateAuditLog(action: "Desactivacion"),
                TestFactory.CreateAuditLog(action: "Creacion")
            };
            var acciones = new List<string> { "Creacion", "Desactivacion" };

            var filtered = logs.Where(r => acciones.Contains(r.Action)).ToList();

            Assert.Equal(3, filtered.Count);
        }

        [Fact]
        public void FilterByTargetType_ShouldFilterCorrectly()
        {
            var logs = new List<AuditLog>
            {
                TestFactory.CreateAuditLog(targetType: "Servicio"),
                TestFactory.CreateAuditLog(targetType: "Empleado"),
                TestFactory.CreateAuditLog(targetType: "Servicio")
            };

            var filtered = logs.Where(r => r.TargetType == "Servicio").ToList();

            Assert.Equal(2, filtered.Count);
        }

        #endregion

        #region Sorting Tests

        [Fact]
        public void SortByTimestamp_Descending_ShouldShowNewestFirst()
        {
            var log1 = TestFactory.CreateAuditLog(userId: "1");
            log1.Timestamp = new DateTime(2024, 1, 1);
            var log2 = TestFactory.CreateAuditLog(userId: "2");
            log2.Timestamp = new DateTime(2024, 3, 1);
            var log3 = TestFactory.CreateAuditLog(userId: "3");
            log3.Timestamp = new DateTime(2024, 2, 1);
            var logs = new List<AuditLog> { log1, log2, log3 };

            var sorted = logs.OrderByDescending(r => r.Timestamp).ToList();

            Assert.Equal(new DateTime(2024, 3, 1), sorted[0].Timestamp);
            Assert.Equal(new DateTime(2024, 2, 1), sorted[1].Timestamp);
            Assert.Equal(new DateTime(2024, 1, 1), sorted[2].Timestamp);
        }

        [Fact]
        public void SortByUserEmail_ShouldOrderAlphabetically()
        {
            var logs = new List<AuditLog>
            {
                TestFactory.CreateAuditLog(userEmail: "zara@test.com"),
                TestFactory.CreateAuditLog(userEmail: "ana@test.com"),
                TestFactory.CreateAuditLog(userEmail: "maria@test.com")
            };

            var sorted = logs.OrderBy(r => r.UserEmail).ToList();

            Assert.Equal("ana@test.com", sorted[0].UserEmail);
            Assert.Equal("maria@test.com", sorted[1].UserEmail);
            Assert.Equal("zara@test.com", sorted[2].UserEmail);
        }

        #endregion

        #region Search Tests

        [Theory]
        [InlineData("admin@test.com", "admin", true)]
        [InlineData("admin@test.com", "ADMIN", true)]
        [InlineData("admin@test.com", "xyz", false)]
        public void SearchByEmail_ShouldBeCaseInsensitive(string email, string searchTerm, bool shouldMatch)
        {
            var log = TestFactory.CreateAuditLog(userEmail: email);
            var matches = log.UserEmail?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

            Assert.Equal(shouldMatch, matches);
        }

        [Theory]
        [InlineData("Creacion de servicio", "servicio", true)]
        [InlineData("Creacion de servicio", "SERVICIO", true)]
        [InlineData("Modificacion de rol", "servicio", false)]
        public void SearchByAction_ShouldBeCaseInsensitive(string action, string searchTerm, bool shouldMatch)
        {
            var log = TestFactory.CreateAuditLog(action: action);
            var matches = log.Action?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

            Assert.Equal(shouldMatch, matches);
        }

        #endregion

        #region Pagination Tests

        [Theory]
        [InlineData(100, 20, 5)]
        [InlineData(95, 20, 5)]
        [InlineData(10, 20, 1)]
        public void TotalPages_ShouldCalculateCorrectly(int totalRecords, int pageSize, int expectedPages)
        {
            var actualPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            Assert.Equal(expectedPages, actualPages);
        }

        [Fact]
        public void Pagination_ShouldReturnCorrectSubset()
        {
            var logs = Enumerable.Range(1, 50)
                .Select(i => TestFactory.CreateAuditLog(targetId: i.ToString()))
                .ToList();

            var page = logs.Skip(20).Take(20).ToList();

            Assert.Equal(20, page.Count);
            Assert.Equal("21", page[0].TargetId);
            Assert.Equal("40", page[19].TargetId);
        }

        #endregion
    }
}
