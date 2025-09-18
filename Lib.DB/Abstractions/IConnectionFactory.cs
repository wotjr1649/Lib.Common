#nullable enable
using Microsoft.Data.SqlClient;

namespace Lib.DB.Abstractions;

/// <summary>
/// SqlConnection 생성 책임을 분리합니다.
/// 테스트/멀티-테넌트/키 로테이션 등에 유리합니다.
/// </summary>
public interface IConnectionFactory
{
    /// <summary>연결 문자열로 SqlConnection 인스턴스를 생성합니다(열지 않음).</summary>
    SqlConnection Create(string connectionString);
}
