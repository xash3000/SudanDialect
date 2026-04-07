export interface AdminLoginRequest {
    username: string;
    password: string;
}

export interface AdminLoginResponse {
    expiresAtUtc: string;
    username: string;
}

export interface AdminAuthSession {
    expiresAtUtc: string;
    username: string;
}
