using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Application.Common.Interfaces;

public interface ICertificateService
{
    Task<CertificateClearanceResultDto> CheckClearanceAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<CertificateDto> GenerateCertificateAsync(GenerateCertificateRequest request, Guid currentUserId, string currentUserName, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateCertificatePdfAsync(Guid certificateId, CancellationToken cancellationToken = default);
    Task<CertificateVerificationDto?> VerifyCertificateAsync(string verificationCode, CancellationToken cancellationToken = default);
    Task<bool> RevokeCertificateAsync(Guid certificateId, string reason, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<List<CertificateDto>> GetStudentCertificatesAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<CertificateDto>> GetCertificatesAsync(Guid? branchId = null, Domain.Enums.CertificateType? type = null, CancellationToken cancellationToken = default);
}
