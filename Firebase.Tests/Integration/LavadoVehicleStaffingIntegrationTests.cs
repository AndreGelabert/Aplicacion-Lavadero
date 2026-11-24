namespace Firebase.Tests.Integration;

public class LavadoVehicleStaffingIntegrationTests
{
    [LocalFirestoreFact]
    public Task CreationPersistsVehicleRequirementAboveLegacyMaximumWithoutTruncation() =>
        new LavadoCreationPricingRefactorTests().CreationPersistsDefaultAndFullVehicleStaffAssignments();
}
