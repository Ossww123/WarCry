using UnityEngine;
using System.Text.RegularExpressions;
using System;

/// <summary>
/// 사용자 입력 검증 및 필터링을 담당하는 유틸리티 클래스
/// 아이디, 비밀번호, 닉네임 등의 입력값 유효성 검사와 실시간 필터링 기능을 제공하며
/// 정규식 기반 검증과 길이 제한, 허용 문자 검사 등을 처리
/// </summary>
public static class InputValidator
{
    #region Constants - Validation Rules

    // 길이 제한
    private const int MinUsernameLength = 4;
    private const int MaxUsernameLength = 12;
    private const int MinPasswordLength = 6;
    private const int MaxPasswordLength = 16;
    private const int MinNicknameLength = 2;
    private const int MaxNicknameLength = 12;

    // 정규식 패턴
    private const string UsernamePattern = @"^[a-z0-9]+$";
    private const string PasswordPattern = @"^[a-zA-Z0-9!@#$%^&*]+$";
    private const string NicknamePattern = @"^[a-zA-Z0-9가-힣]+$";

    // 필터링 패턴 (허용되지 않는 문자 제거용)
    private const string UsernameFilterPattern = @"[^a-z0-9]";
    private const string PasswordFilterPattern = @"[^a-zA-Z0-9!@#$%^&*]";
    private const string NicknameFilterPattern = @"[^a-zA-Z0-9가-힣]";

    // 에러 메시지
    private const string UsernameEmptyError = "아이디를 입력해주세요.";
    private const string UsernameFormatError = "아이디는 영문 소문자와 숫자만 사용 가능합니다.";
    private const string UsernameLengthError = "아이디는 4~12자여야 합니다.";

    private const string PasswordEmptyError = "비밀번호를 입력해주세요.";
    private const string PasswordFormatError = "비밀번호는 영문, 숫자, 특수문자(!@#$%^&*)만 사용 가능합니다.";
    private const string PasswordLengthError = "비밀번호는 6~16자여야 합니다.";

    private const string NicknameEmptyError = "닉네임을 입력해주세요.";
    private const string NicknameFormatError = "닉네임은 영문, 숫자, 한글만 사용 가능합니다.";
    private const string NicknameLengthError = "닉네임은 2~12자여야 합니다.";

    private const string PasswordMismatchError = "비밀번호가 일치하지 않습니다.";

    #endregion

    #region Data Structures

    /// <summary>
    /// 입력 검증 결과
    /// </summary>
    public struct ValidationResult
    {
        public bool IsValid;
        public string ErrorMessage;
        public string FilteredValue;

        public ValidationResult(bool isValid, string errorMessage = "", string filteredValue = "")
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            FilteredValue = filteredValue;
        }
    }

    /// <summary>
    /// 입력 필터링 결과
    /// </summary>
    public struct FilterResult
    {
        public string FilteredValue;
        public bool WasChanged;

        public FilterResult(string filteredValue, bool wasChanged)
        {
            FilteredValue = filteredValue;
            WasChanged = wasChanged;
        }
    }

    #endregion

    #region Public API - Username Validation

    /// <summary>
    /// 아이디 입력값 검증
    /// </summary>
    /// <param name="username">검증할 아이디</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidateUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return new ValidationResult(false, UsernameEmptyError);
        }

        if (!IsValidUsernameLength(username))
        {
            return new ValidationResult(false, UsernameLengthError);
        }

        if (!IsValidUsernameFormat(username))
        {
            return new ValidationResult(false, UsernameFormatError);
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// 아이디 실시간 필터링 (입력 중 허용되지 않는 문자 제거)
    /// </summary>
    /// <param name="input">입력값</param>
    /// <returns>필터링 결과</returns>
    public static FilterResult FilterUsernameInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new FilterResult("", false);
        }

        string filtered = Regex.Replace(input, UsernameFilterPattern, "");
        filtered = ApplyLengthLimit(filtered, MaxUsernameLength);

        bool wasChanged = filtered != input;
        return new FilterResult(filtered, wasChanged);
    }

    #endregion

    #region Public API - Password Validation

    /// <summary>
    /// 비밀번호 입력값 검증
    /// </summary>
    /// <param name="password">검증할 비밀번호</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new ValidationResult(false, PasswordEmptyError);
        }

        if (!IsValidPasswordLength(password))
        {
            return new ValidationResult(false, PasswordLengthError);
        }

        if (!IsValidPasswordFormat(password))
        {
            return new ValidationResult(false, PasswordFormatError);
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// 비밀번호 확인 검증 (비밀번호 일치 여부)
    /// </summary>
    /// <param name="password">원본 비밀번호</param>
    /// <param name="confirmPassword">확인 비밀번호</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidatePasswordConfirm(string password, string confirmPassword)
    {
        if (string.IsNullOrEmpty(confirmPassword))
        {
            return new ValidationResult(false, PasswordEmptyError);
        }

        if (password != confirmPassword)
        {
            return new ValidationResult(false, PasswordMismatchError);
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// 비밀번호 실시간 필터링
    /// </summary>
    /// <param name="input">입력값</param>
    /// <returns>필터링 결과</returns>
    public static FilterResult FilterPasswordInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new FilterResult("", false);
        }

        string filtered = Regex.Replace(input, PasswordFilterPattern, "");
        filtered = ApplyLengthLimit(filtered, MaxPasswordLength);

        bool wasChanged = filtered != input;
        return new FilterResult(filtered, wasChanged);
    }

    #endregion

    #region Public API - Nickname Validation

    /// <summary>
    /// 닉네임 입력값 검증
    /// </summary>
    /// <param name="nickname">검증할 닉네임</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidateNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            return new ValidationResult(false, NicknameEmptyError);
        }

        if (!IsValidNicknameLength(nickname))
        {
            return new ValidationResult(false, NicknameLengthError);
        }

        if (!IsValidNicknameFormat(nickname))
        {
            return new ValidationResult(false, NicknameFormatError);
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// 닉네임 실시간 필터링
    /// </summary>
    /// <param name="input">입력값</param>
    /// <returns>필터링 결과</returns>
    public static FilterResult FilterNicknameInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new FilterResult("", false);
        }

        string filtered = Regex.Replace(input, NicknameFilterPattern, "");
        filtered = ApplyLengthLimit(filtered, MaxNicknameLength);

        bool wasChanged = filtered != input;
        return new FilterResult(filtered, wasChanged);
    }

    #endregion

    #region Public API - Comprehensive Validation

    /// <summary>
    /// 로그인 폼 전체 검증
    /// </summary>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidateLoginForm(string username, string password)
    {
        var usernameResult = ValidateUsername(username);
        if (!usernameResult.IsValid)
        {
            return usernameResult;
        }

        var passwordResult = ValidatePassword(password);
        if (!passwordResult.IsValid)
        {
            return passwordResult;
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// 회원가입 폼 전체 검증
    /// </summary>
    /// <param name="username">아이디</param>
    /// <param name="password">비밀번호</param>
    /// <param name="confirmPassword">비밀번호 확인</param>
    /// <param name="nickname">닉네임</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidateSignupForm(string username, string password, string confirmPassword, string nickname)
    {
        var usernameResult = ValidateUsername(username);
        if (!usernameResult.IsValid)
        {
            return usernameResult;
        }

        var passwordResult = ValidatePassword(password);
        if (!passwordResult.IsValid)
        {
            return passwordResult;
        }

        var confirmResult = ValidatePasswordConfirm(password, confirmPassword);
        if (!confirmResult.IsValid)
        {
            return confirmResult;
        }

        var nicknameResult = ValidateNickname(nickname);
        if (!nicknameResult.IsValid)
        {
            return nicknameResult;
        }

        return new ValidationResult(true);
    }

    #endregion

    #region Username Validation Helpers

    /// <summary>
    /// 아이디 길이 검증
    /// </summary>
    /// <param name="username">검증할 아이디</param>
    /// <returns>유효한 길이면 true</returns>
    private static bool IsValidUsernameLength(string username)
    {
        return username.Length >= MinUsernameLength && username.Length <= MaxUsernameLength;
    }

    /// <summary>
    /// 아이디 형식 검증 (영문 소문자, 숫자만)
    /// </summary>
    /// <param name="username">검증할 아이디</param>
    /// <returns>유효한 형식이면 true</returns>
    private static bool IsValidUsernameFormat(string username)
    {
        return Regex.IsMatch(username, UsernamePattern);
    }

    #endregion

    #region Password Validation Helpers

    /// <summary>
    /// 비밀번호 길이 검증
    /// </summary>
    /// <param name="password">검증할 비밀번호</param>
    /// <returns>유효한 길이면 true</returns>
    private static bool IsValidPasswordLength(string password)
    {
        return password.Length >= MinPasswordLength && password.Length <= MaxPasswordLength;
    }

    /// <summary>
    /// 비밀번호 형식 검증 (영문, 숫자, 특수문자)
    /// </summary>
    /// <param name="password">검증할 비밀번호</param>
    /// <returns>유효한 형식이면 true</returns>
    private static bool IsValidPasswordFormat(string password)
    {
        return Regex.IsMatch(password, PasswordPattern);
    }

    #endregion

    #region Nickname Validation Helpers

    /// <summary>
    /// 닉네임 길이 검증
    /// </summary>
    /// <param name="nickname">검증할 닉네임</param>
    /// <returns>유효한 길이면 true</returns>
    private static bool IsValidNicknameLength(string nickname)
    {
        return nickname.Length >= MinNicknameLength && nickname.Length <= MaxNicknameLength;
    }

    /// <summary>
    /// 닉네임 형식 검증 (영문, 숫자, 한글)
    /// </summary>
    /// <param name="nickname">검증할 닉네임</param>
    /// <returns>유효한 형식이면 true</returns>
    private static bool IsValidNicknameFormat(string nickname)
    {
        return Regex.IsMatch(nickname, NicknamePattern);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 문자열 길이 제한 적용
    /// </summary>
    /// <param name="input">입력 문자열</param>
    /// <param name="maxLength">최대 길이</param>
    /// <returns>길이 제한이 적용된 문자열</returns>
    private static string ApplyLengthLimit(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return input.Length > maxLength ? input.Substring(0, maxLength) : input;
    }

    #endregion

    #region Public API - Utility Methods

    /// <summary>
    /// 입력 타입에 따른 자동 필터링
    /// </summary>
    /// <param name="input">입력값</param>
    /// <param name="inputType">입력 타입 ("username", "password", "nickname")</param>
    /// <returns>필터링 결과</returns>
    public static FilterResult FilterInputByType(string input, string inputType)
    {
        switch (inputType.ToLower())
        {
            case "username":
                return FilterUsernameInput(input);
            case "password":
                return FilterPasswordInput(input);
            case "nickname":
                return FilterNicknameInput(input);
            default:
                return new FilterResult(input, false);
        }
    }

    /// <summary>
    /// 입력 타입에 따른 자동 검증
    /// </summary>
    /// <param name="input">입력값</param>
    /// <param name="inputType">입력 타입 ("username", "password", "nickname")</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidateInputByType(string input, string inputType)
    {
        switch (inputType.ToLower())
        {
            case "username":
                return ValidateUsername(input);
            case "password":
                return ValidatePassword(input);
            case "nickname":
                return ValidateNickname(input);
            default:
                return new ValidationResult(true);
        }
    }

    /// <summary>
    /// 입력값이 비어있는지 확인
    /// </summary>
    /// <param name="inputs">확인할 입력값들</param>
    /// <returns>모든 입력값이 채워져 있으면 true</returns>
    public static bool AreAllFieldsFilled(params string[] inputs)
    {
        foreach (string input in inputs)
        {
            if (string.IsNullOrEmpty(input?.Trim()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 안전한 문자열 비교 (null 체크 포함)
    /// </summary>
    /// <param name="str1">첫 번째 문자열</param>
    /// <param name="str2">두 번째 문자열</param>
    /// <returns>두 문자열이 동일하면 true</returns>
    public static bool SafeStringEquals(string str1, string str2)
    {
        return string.Equals(str1 ?? "", str2 ?? "", StringComparison.Ordinal);
    }

    #endregion

    #region Debug Utilities

    /// <summary>
    /// 검증 규칙 정보 가져오기 (디버깅용)
    /// </summary>
    /// <param name="inputType">입력 타입</param>
    /// <returns>해당 타입의 검증 규칙 설명</returns>
    public static string GetValidationRules(string inputType)
    {
        switch (inputType.ToLower())
        {
            case "username":
                return $"아이디: {MinUsernameLength}~{MaxUsernameLength}자, 영문 소문자와 숫자만 허용";
            case "password":
                return $"비밀번호: {MinPasswordLength}~{MaxPasswordLength}자, 영문, 숫자, 특수문자(!@#$%^&*) 허용";
            case "nickname":
                return $"닉네임: {MinNicknameLength}~{MaxNicknameLength}자, 영문, 숫자, 한글 허용";
            default:
                return "알 수 없는 입력 타입";
        }
    }

    #endregion
}