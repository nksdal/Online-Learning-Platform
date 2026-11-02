import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Assignment, AssignmentSubmission, Category, Course, CourseProgress, DiscussionPost, Enrollment, LearningStreak, Lesson, MessageResponse, RecommendedCourse, TokenResponse, UserProfile } from './models';

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
  myStreak() { return this.http.get<LearningStreak>(`${this.root}/api/users/me/streak`); }
  recommendations() { return this.http.get<RecommendedCourse[]>(`${this.root}/api/users/me/recommendations`); }
  lessons(courseId: string) { return this.http.get<Lesson[]>(`${this.root}/api/courses/${courseId}/lessons`); }
  courseProgress(courseId: string) { return this.http.get<CourseProgress>(`${this.root}/api/courses/${courseId}/progress/me`); }
  assignments(courseId: string) { return this.http.get<Assignment[]>(`${this.root}/api/courses/${courseId}/assignments`); }
  mySubmissions(assignmentId: string) { return this.http.get<AssignmentSubmission[]>(`${this.root}/api/assignments/${assignmentId}/submissions/me`); }
  submitAssignment(assignmentId: string, text: string) { return this.http.post<AssignmentSubmission>(`${this.root}/api/assignments/${assignmentId}/submissions`, { text }); }
  discussions(courseId: string) { return this.http.get<DiscussionPost[]>(`${this.root}/api/courses/${courseId}/discussions`); }
  createDiscussionPost(courseId: string, body: string) { return this.http.post<DiscussionPost>(`${this.root}/api/courses/${courseId}/discussions`, { body }); }
  replyToDiscussion(postId: string, body: string) { return this.http.post<DiscussionPost>(`${this.root}/api/discussions/${postId}/replies`, { body }); }
  completeLesson(lessonId: string) { return this.http.put<void>(`${this.root}/api/lessons/${lessonId}/progress`, {}); }
  resetLesson(lessonId: string) { return this.http.delete<void>(`${this.root}/api/lessons/${lessonId}/progress`); }
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
