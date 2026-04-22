using JSSP.Core.Models;

namespace JSSP.Tests.Core;

[TestClass]
public class OperationTests
{
    [TestMethod]
    public void ToString_ContainsAllFields()
    {
        // Arrange
        var operation = new Operation(1, 2, "Machining", 30);

        // Act
        string result = operation.ToString();

        // Assert
        Assert.Contains("JobId: 1", result);
        Assert.Contains("OperationId: 2", result);
        Assert.Contains("Subdivision: Machining", result);
        Assert.Contains("ProcessingTime: 30", result);
    }
}
