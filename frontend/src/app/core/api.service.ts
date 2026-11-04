import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AdminUserPage, Assignment, AssignmentSubmission, Category, Course, CourseProgress, DiscussionPost, Enrollment, LearningStreak, Lesson, MessageResponse, RecommendedCourse, TeacherAssignmentSubmission, TokenResponse, UserProfile } from './models';

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
  updateProfile(firstName: string, lastName: string, timeZoneId: string) {
    return this.http.put<UserProfile>(`${this.root}/api/users/me/profile`, { firstName, lastName, timeZoneId });
  }
  uploadProfilePicture(file: File) {
    const body = new FormData();
    body.append('file', file);
    return this.http.put<UserProfile>(`${this.root}/api/users/me/profile-picture`, body);
  }
  removeProfilePicture() {
    return this.http.delete<UserProfile>(`${this.root}/api/users/me/profile-picture`);
  }
  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<TokenResponse>(`${this.root}/api/users/me/password`, { currentPassword, newPassword });
  }

  adminUsers(search = '', page = 1, pageSize = 50) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<AdminUserPage>(`${this.root}/api/admin/users`, { params });
  }
  adminChangeUserRole(id: string, role: string) {
    return this.http.put<void>(`${this.root}/api/admin/users/${id}/role`, { role });
  }
  adminSetUserStatus(id: string, isActive: boolean) {
    return this.http.put<void>(`${this.root}/api/admin/users/${id}/status`, { isActive });
  }
  adminCategories() { return this.http.get<Category[]>(`${this.root}/api/admin/categories`); }
  adminCreateCategory(name: string, description: string) {
    return this.http.post<Category>(`${this.root}/api/admin/categories`, { name, description });
  }
  adminUpdateCategory(id: string, name: string, description: string) {
    return this.http.put<Category>(`${this.root}/api/admin/categories/${id}`, { name, description });
  }
  adminSetCategoryStatus(id: string, isActive: boolean) {
    return this.http.put<Category>(`${this.root}/api/admin/categories/${id}/status`, { isActive });
  }
  adminCourses() { return this.http.get<Course[]>(`${this.root}/api/admin/courses`); }
  adminPendingCourses() { return this.http.get<Course[]>(`${this.root}/api/admin/courses/pending`); }
  adminApproveCourse(id: string) { return this.http.post<Course>(`${this.root}/api/admin/courses/${id}/approve`, {}); }
  adminRejectCourse(id: string, reason: string) {
    return this.http.post<Course>(`${this.root}/api/admin/courses/${id}/reject`, { reason });
  }
  adminCourseDiscussions(id: string) { return this.http.get<DiscussionPost[]>(`${this.root}/api/admin/courses/${id}/discussions`); }
  adminModerateDiscussion(id: string, isHidden: boolean, reason: string) {
    return this.http.put<DiscussionPost>(`${this.root}/api/admin/discussions/${id}/moderation`, { isHidden, reason: reason || null });
  }

  teacherCourses() { return this.http.get<Course[]>(`${this.root}/api/teacher/courses`); }
  createTeacherCourse(data: Pick<Course, 'categoryId' | 'title' | 'summary' | 'description'>) {
    return this.http.post<Course>(`${this.root}/api/teacher/courses`, data);
  }
  updateTeacherCourse(id: string, data: Pick<Course, 'categoryId' | 'title' | 'summary' | 'description'>) {
    return this.http.put<Course>(`${this.root}/api/teacher/courses/${id}`, data);
  }
  submitTeacherCourse(id: string) { return this.http.post<Course>(`${this.root}/api/teacher/courses/${id}/submit`, {}); }
  archiveTeacherCourse(id: string) { return this.http.post<Course>(`${this.root}/api/teacher/courses/${id}/archive`, {}); }
  teacherLessons(courseId: string) { return this.http.get<Lesson[]>(`${this.root}/api/teacher/courses/${courseId}/lessons`); }
  createTeacherLesson(courseId: string, data: Pick<Lesson, 'title' | 'content' | 'position'>) {
    return this.http.post<Lesson>(`${this.root}/api/teacher/courses/${courseId}/lessons`, data);
  }
  updateTeacherLesson(id: string, data: Pick<Lesson, 'title' | 'content' | 'position'>) {
    return this.http.put<Lesson>(`${this.root}/api/teacher/lessons/${id}`, data);
  }
  deleteTeacherLesson(id: string) { return this.http.delete<void>(`${this.root}/api/teacher/lessons/${id}`); }
  teacherAssignments(courseId: string) { return this.http.get<Assignment[]>(`${this.root}/api/teacher/courses/${courseId}/assignments`); }
  createTeacherAssignment(courseId: string, data: Pick<Assignment, 'title' | 'instructions' | 'dueAtUtc' | 'maxAttempts' | 'maxScore'>) {
    return this.http.post<Assignment>(`${this.root}/api/teacher/courses/${courseId}/assignments`, data);
  }
  updateTeacherAssignment(id: string, data: Pick<Assignment, 'title' | 'instructions' | 'dueAtUtc' | 'maxAttempts' | 'maxScore'>) {
    return this.http.put<Assignment>(`${this.root}/api/teacher/assignments/${id}`, data);
  }
  teacherSubmissions(assignmentId: string) {
    return this.http.get<TeacherAssignmentSubmission[]>(`${this.root}/api/teacher/assignments/${assignmentId}/submissions`);
  }
  gradeTeacherSubmission(id: string, score: number, feedback: string) {
    return this.http.put<TeacherAssignmentSubmission>(`${this.root}/api/submissions/${id}/grade`, { score, feedback });
  }
  teacherDiscussions(courseId: string) { return this.http.get<DiscussionPost[]>(`${this.root}/api/teacher/courses/${courseId}/discussions`); }
  moderateTeacherDiscussion(id: string, isHidden: boolean, reason: string) {
    return this.http.put<DiscussionPost>(`${this.root}/api/teacher/discussions/${id}/moderation`, { isHidden, reason: reason || null });
  }

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
