using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Maui.ViewModels;

public partial class StudentsViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly ClientAuthState _authState;

    public StudentsViewModel(IApiClient apiClient, ClientAuthState authState)
    {
        _apiClient = apiClient;
        _authState = authState;
        Title = "Student Directory & Admissions";
    }

    public ObservableCollection<StudentSummaryDto> Students { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedGrade { get; set; }

    [ObservableProperty]
    public partial StudentSummaryDto? SelectedStudent { get; set; }

    [ObservableProperty]
    public partial StudentDetailsDto? CurrentStudentDetails { get; set; }

    [ObservableProperty]
    public partial bool IsAdmitModalVisible { get; set; } = false;

    [ObservableProperty]
    public partial bool IsDetailsModalVisible { get; set; } = false;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalStudentsCount { get; set; }

    // New Student Form Fields
    [ObservableProperty]
    public partial string NewFirstName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewLastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewGrade { get; set; } = "Grade 1";

    [ObservableProperty]
    public partial string NewSection { get; set; } = "A";

    [ObservableProperty]
    public partial string NewRollNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime NewDateOfBirth { get; set; } = DateTime.Today.AddYears(-6);

    [ObservableProperty]
    public partial Gender NewGender { get; set; } = Gender.Male;

    [ObservableProperty]
    public partial string NewBloodGroup { get; set; } = "O+";

    [ObservableProperty]
    public partial string NewNationalId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewCity { get; set; } = "Islamabad";

    // Initial Guardian Form Fields
    [ObservableProperty]
    public partial string NewGuardianFirstName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewGuardianLastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewGuardianPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewGuardianEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewGuardianNationalId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewGuardianOccupation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial GuardianRelationshipType NewGuardianRelationship { get; set; } = GuardianRelationshipType.Father;

    [RelayCommand]
    public async Task LoadStudentsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        StatusMessage = string.Empty;

        try
        {
            var items = await _apiClient.GetStudentsAsync(
                search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                grade: string.IsNullOrWhiteSpace(SelectedGrade) ? null : SelectedGrade,
                pageSize: 100);

            Students.Clear();
            foreach (var student in items)
            {
                Students.Add(student);
            }
            TotalStudentsCount = Students.Count;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load students: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SearchStudentsAsync()
    {
        await LoadStudentsAsync();
    }

    [RelayCommand]
    public void OpenAdmitModal()
    {
        ClearError();
        StatusMessage = string.Empty;
        NewFirstName = string.Empty;
        NewLastName = string.Empty;
        NewRollNumber = string.Empty;
        NewAddress = string.Empty;
        NewNationalId = string.Empty;
        NewGuardianFirstName = string.Empty;
        NewGuardianLastName = string.Empty;
        NewGuardianPhone = string.Empty;
        NewGuardianEmail = string.Empty;
        NewGuardianNationalId = string.Empty;
        NewGuardianOccupation = string.Empty;
        IsAdmitModalVisible = true;
    }

    [RelayCommand]
    public void CloseAdmitModal()
    {
        IsAdmitModalVisible = false;
    }

    [RelayCommand]
    public async Task SubmitAdmissionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFirstName) || string.IsNullOrWhiteSpace(NewLastName))
        {
            ErrorMessage = "Student First Name and Last Name are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewGrade))
        {
            ErrorMessage = "Grade/Class is required.";
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            CreateGuardianRequestDto? initialGuardian = null;
            if (!string.IsNullOrWhiteSpace(NewGuardianFirstName) && !string.IsNullOrWhiteSpace(NewGuardianPhone))
            {
                initialGuardian = new CreateGuardianRequestDto(
                    BranchId: _authState.CurrentUser?.BranchId,
                    FirstName: NewGuardianFirstName.Trim(),
                    LastName: NewGuardianLastName.Trim(),
                    RelationshipType: NewGuardianRelationship,
                    NationalId: string.IsNullOrWhiteSpace(NewGuardianNationalId) ? null : NewGuardianNationalId.Trim(),
                    Email: string.IsNullOrWhiteSpace(NewGuardianEmail) ? null : NewGuardianEmail.Trim(),
                    Phone: NewGuardianPhone.Trim(),
                    AlternatePhone: null,
                    Occupation: string.IsNullOrWhiteSpace(NewGuardianOccupation) ? null : NewGuardianOccupation.Trim(),
                    AnnualIncome: null,
                    Address: string.IsNullOrWhiteSpace(NewAddress) ? null : NewAddress.Trim(),
                    City: string.IsNullOrWhiteSpace(NewCity) ? null : NewCity.Trim(),
                    IsEmergencyContact: true
                );
            }

            var request = new CreateStudentRequestDto(
                BranchId: _authState.CurrentUser?.BranchId,
                AdmissionNumber: null, // Auto-generate on server
                RollNumber: string.IsNullOrWhiteSpace(NewRollNumber) ? null : NewRollNumber.Trim(),
                FirstName: NewFirstName.Trim(),
                LastName: NewLastName.Trim(),
                DateOfBirth: NewDateOfBirth,
                Gender: NewGender,
                BloodGroup: string.IsNullOrWhiteSpace(NewBloodGroup) ? null : NewBloodGroup.Trim(),
                NationalIdOrBForm: string.IsNullOrWhiteSpace(NewNationalId) ? null : NewNationalId.Trim(),
                AdmissionDate: DateTime.UtcNow,
                CurrentGrade: NewGrade.Trim(),
                Section: string.IsNullOrWhiteSpace(NewSection) ? null : NewSection.Trim(),
                EmergencyContactName: string.IsNullOrWhiteSpace(NewGuardianFirstName) ? null : $"{NewGuardianFirstName} {NewGuardianLastName}".Trim(),
                EmergencyContactPhone: string.IsNullOrWhiteSpace(NewGuardianPhone) ? null : NewGuardianPhone.Trim(),
                EmergencyContactRelationship: NewGuardianRelationship.ToString(),
                Address: string.IsNullOrWhiteSpace(NewAddress) ? null : NewAddress.Trim(),
                City: string.IsNullOrWhiteSpace(NewCity) ? null : NewCity.Trim(),
                MedicalNotes: null,
                InitialGuardian: initialGuardian,
                GuardianRelationship: NewGuardianRelationship.ToString(),
                IsPrimaryGuardian: true
            );

            var (success, message, studentId) = await _apiClient.CreateStudentAsync(request);
            if (success)
            {
                StatusMessage = "Student admitted successfully!";
                IsAdmitModalVisible = false;
                await LoadStudentsAsync();
            }
            else
            {
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error admitting student: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ViewStudentDetailsAsync(StudentSummaryDto student)
    {
        if (student == null) return;
        IsBusy = true;
        ClearError();

        try
        {
            var details = await _apiClient.GetStudentByIdAsync(student.Id);
            if (details != null)
            {
                CurrentStudentDetails = details;
                IsDetailsModalVisible = true;
            }
            else
            {
                ErrorMessage = "Could not load student profile.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading profile: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void CloseDetailsModal()
    {
        IsDetailsModalVisible = false;
        CurrentStudentDetails = null;
    }

    [RelayCommand]
    public async Task DeleteStudentAsync(StudentSummaryDto student)
    {
        if (student == null) return;
        IsBusy = true;
        ClearError();

        try
        {
            var (success, message) = await _apiClient.DeleteStudentAsync(student.Id);
            if (success)
            {
                StatusMessage = $"Student '{student.FullName}' record deleted.";
                await LoadStudentsAsync();
            }
            else
            {
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting student: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("//DashboardPage");
    }
}
