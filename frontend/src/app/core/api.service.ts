import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Category, Course, Enrollment, MessageResponse, TokenResponse, UserProfile } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly root = environment.apiUrl;

  categories() { return this.http.get<Category[]>(`${this.root}/api/categories`); }
  courses(categoryId?: string) {
    const params = categoryId ? new HttpParams().set('categoryId', categoryId) : undefined;
    return this.http.get<Course[]>(`${this.root}/api/courses`, { params });
  }
  course(id: string) { return this.http.get<Course>(`${this.root}/api/courses/${id}`); }
  enroll(courseId: string) { return this.http.post<Enrollment>(`${this.root}/api/courses/${courseId}/enrollments`, {}); }
  myEnrollments() { return this.http.get<Enrollment[]>(`${this.root}/api/users/me/enrollments`); }
  profile() { return this.http.get<UserProfile>(`${this.root}/api/users/me`); }

  login(email: string, password: string) {
    return this.http.post<TokenResponse>(`${this.root}/api/auth/login`, { email, password });
  }
  register(data: { email: string; firstName: string; lastName: string; password: string; timeZoneId: string }) {
    return this.http.post<MessageResponse>(`${this.root}/api/auth/register`, data);
  }
  verifyEmail(userId: string, token: string) {
    return this.http.post<MessageResponse>(`${this.root}/api/auth/verify-email`, { userId, token });
  }
  forgotPassword(email: string) {
    return this.http.post<MessageResponse>(`${this.root}/api/auth/forgot-password`, { email });
  }
  resetPassword(userId: string, token: string, newPassword: string) {
    return this.http.post<MessageResponse>(`${this.root}/api/auth/reset-password`, { userId, token, newPassword });
  }
  resendVerification(email: string) {
    return this.http.post<MessageResponse>(`${this.root}/api/auth/resend-verification`, { email });
  }
  refresh() {
    return this.http.post<TokenResponse>(`${this.root}/api/auth/refresh`, {}, {
      headers: { 'X-Refresh-Request': '1' }, withCredentials: true,
    });
  }
  logout() {
    return this.http.post<void>(`${this.root}/api/auth/logout`, {}, {
      headers: { 'X-Refresh-Request': '1' }, withCredentials: true,
    });
  }
}
