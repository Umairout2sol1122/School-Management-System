using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Infrastructure.Services.Payroll;

public static class PayrollTaxCalculator
{
    public const decimal MinimumWagePakistan = 37000m;
    public const decimal DefaultEobiEmployeeMonthly = 370m; // 1% of PKR 37,000
    public const decimal DefaultEobiEmployerMonthly = 1850m; // 5% of PKR 37,000
    public const int DefaultWorkingDaysPerMonth = 26;

    public static TaxCalculationResult CalculatePakistaniIncomeTax(decimal annualGross)
    {
        if (annualGross <= 600000m)
        {
            return new TaxCalculationResult(
                AnnualTaxableSalary: annualGross,
                AnnualTax: 0m,
                MonthlyTax: 0m,
                SlabDescription: "Exempt (Up to PKR 600,000 / year)");
        }

        if (annualGross <= 1200000m)
        {
            decimal taxableExcess = annualGross - 600000m;
            decimal annualTax = Math.Round(taxableExcess * 0.05m, 2);
            decimal monthlyTax = Math.Round(annualTax / 12m, 2);
            return new TaxCalculationResult(
                AnnualTaxableSalary: annualGross,
                AnnualTax: annualTax,
                MonthlyTax: monthlyTax,
                SlabDescription: "5% of amount exceeding PKR 600,000");
        }

        if (annualGross <= 2200000m)
        {
            decimal taxableExcess = annualGross - 1200000m;
            decimal annualTax = Math.Round(30000m + (taxableExcess * 0.15m), 2);
            decimal monthlyTax = Math.Round(annualTax / 12m, 2);
            return new TaxCalculationResult(
                AnnualTaxableSalary: annualGross,
                AnnualTax: annualTax,
                MonthlyTax: monthlyTax,
                SlabDescription: "PKR 30,000 + 15% of amount exceeding PKR 1,200,000");
        }

        if (annualGross <= 3200000m)
        {
            decimal taxableExcess = annualGross - 2200000m;
            decimal annualTax = Math.Round(180000m + (taxableExcess * 0.25m), 2);
            decimal monthlyTax = Math.Round(annualTax / 12m, 2);
            return new TaxCalculationResult(
                AnnualTaxableSalary: annualGross,
                AnnualTax: annualTax,
                MonthlyTax: monthlyTax,
                SlabDescription: "PKR 180,000 + 25% of amount exceeding PKR 2,200,000");
        }

        if (annualGross <= 4100000m)
        {
            decimal taxableExcess = annualGross - 3200000m;
            decimal annualTax = Math.Round(430000m + (taxableExcess * 0.30m), 2);
            decimal monthlyTax = Math.Round(annualTax / 12m, 2);
            return new TaxCalculationResult(
                AnnualTaxableSalary: annualGross,
                AnnualTax: annualTax,
                MonthlyTax: monthlyTax,
                SlabDescription: "PKR 430,000 + 30% of amount exceeding PKR 3,200,000");
        }
        else
        {
            decimal taxableExcess = annualGross - 4100000m;
            decimal annualTax = Math.Round(700000m + (taxableExcess * 0.35m), 2);
            decimal monthlyTax = Math.Round(annualTax / 12m, 2);
            return new TaxCalculationResult(
                AnnualTaxableSalary: annualGross,
                AnnualTax: annualTax,
                MonthlyTax: monthlyTax,
                SlabDescription: "PKR 700,000 + 35% of amount exceeding PKR 4,100,000");
        }
    }

    public static (decimal EmployeeEobi, decimal EmployerEobi) CalculateEobi(bool eobiEnabled)
    {
        if (!eobiEnabled) return (0m, 0m);
        return (DefaultEobiEmployeeMonthly, DefaultEobiEmployerMonthly);
    }

    public static decimal CalculateAbsenceDeduction(decimal grossSalary, int absenceDays, int workingDays = DefaultWorkingDaysPerMonth)
    {
        if (absenceDays <= 0 || grossSalary <= 0m || workingDays <= 0) return 0m;
        decimal dailyRate = grossSalary / (decimal)workingDays;
        return Math.Round(dailyRate * absenceDays, 2);
    }
}
