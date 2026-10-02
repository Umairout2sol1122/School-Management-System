using FluentAssertions;
using SchoolManagement.Infrastructure.Services.Payroll;
using Xunit;

namespace SchoolManagement.UnitTests;

public class PayrollTaxCalculatorTests
{
    [Theory]
    [InlineData(400000, 0, 0)]      // Under 600k -> 0%
    [InlineData(600000, 0, 0)]      // Exact 600k -> 0%
    public void CalculatePakistaniIncomeTax_ExemptSlab_ReturnsZeroTax(decimal annualGross, decimal expectedAnnual, decimal expectedMonthly)
    {
        var result = PayrollTaxCalculator.CalculatePakistaniIncomeTax(annualGross);

        result.AnnualTax.Should().Be(expectedAnnual);
        result.MonthlyTax.Should().Be(expectedMonthly);
        result.SlabDescription.Should().Contain("Exempt");
    }

    [Fact]
    public void CalculatePakistaniIncomeTax_SecondSlab_Calculates5PercentOnExcess()
    {
        // 900,000 annual -> 300,000 excess * 5% = 15,000 annual tax -> 1,250 monthly
        var result = PayrollTaxCalculator.CalculatePakistaniIncomeTax(900000m);

        result.AnnualTax.Should().Be(15000m);
        result.MonthlyTax.Should().Be(1250m);
        result.SlabDescription.Should().Contain("5%");
    }

    [Fact]
    public void CalculatePakistaniIncomeTax_ThirdSlab_Calculates30KPlus15Percent()
    {
        // 1,800,000 annual -> 30,000 + (600,000 * 0.15 = 90,000) = 120,000 annual tax -> 10,000 monthly
        var result = PayrollTaxCalculator.CalculatePakistaniIncomeTax(1800000m);

        result.AnnualTax.Should().Be(1200000m * 0.10m); // 120,000
        result.MonthlyTax.Should().Be(10000m);
        result.SlabDescription.Should().Contain("15%");
    }

    [Fact]
    public void CalculateEobi_WhenEnabled_ReturnsStatutoryContributions()
    {
        var (emp, empr) = PayrollTaxCalculator.CalculateEobi(true);

        emp.Should().Be(370m);
        empr.Should().Be(1850m);
    }

    [Fact]
    public void CalculateEobi_WhenDisabled_ReturnsZero()
    {
        var (emp, empr) = PayrollTaxCalculator.CalculateEobi(false);

        emp.Should().Be(0m);
        empr.Should().Be(0m);
    }

    [Fact]
    public void CalculateAbsenceDeduction_CalculatesProRataDailyGross()
    {
        // Gross 130,000, 26 working days = 5,000/day. 3 days absent = 15,000 deduction
        decimal deduction = PayrollTaxCalculator.CalculateAbsenceDeduction(130000m, 3, 26);

        deduction.Should().Be(15000m);
    }

    [Fact]
    public void CalculateAbsenceDeduction_WhenZeroDays_ReturnsZero()
    {
        decimal deduction = PayrollTaxCalculator.CalculateAbsenceDeduction(100000m, 0, 26);

        deduction.Should().Be(0m);
    }
}
