import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AdminAnalytics, AdminUserSummary, Category, Course, DiscussionPost } from '../core/models';

type AdminTab = 'overview' | 'analytics' | 'courses' | 'users' | 'categories' | 'discussions';

function apiMessage(error: any, fallback: string): string {
  return error?.error?.title ?? error?.error?.detail ?? fallback;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="admin-page">
      <header class="admin-hero"><div><span class="admin-eyebrow">NIKLADORI · ADMINISTRATION</span><h1>A clear view of<br><em>what’s growing.</em></h1><p>Review courses, support your community, and keep the learning space healthy.</p></div><span class="admin-hero-mark" aria-hidden="true">N</span></header>
      @if (notice()) { <div class="admin-notice" role="status">{{ notice() }}</div> }
      @if (error()) { <div class="admin-error" role="alert">{{ error() }} <button type="button" (click)="error.set('')" aria-label="Dismiss">×</button></div> }

      <section class="admin-stats" aria-label="Platform overview">
        <article><span>COURSES IN REVIEW</span><b>{{ pendingCourses().length }}</b><small>Awaiting a decision</small></article>
        <article><span>ALL COURSES</span><b>{{ allCourses().length }}</b><small>Across every status</small></article>
        <article><span>REGISTERED USERS</span><b>{{ userTotal() }}</b><small>Matching current list</small></article>
        <article><span>SUBJECT CATEGORIES</span><b>{{ categories().length }}</b><small>{{ activeCategoryCount() }} active</small></article>
      </section>

      <nav class="admin-tabs" aria-label="Administration sections">
        <button [class.active]="tab() === 'overview'" (click)="setTab('overview')">Overview</button>
        <button [class.active]="tab() === 'analytics'" (click)="setTab('analytics')">Analytics</button>
        <button [class.active]="tab() === 'courses'" (click)="setTab('courses')">Courses <span>{{ pendingCourses().length }}</span></button>
        <button [class.active]="tab() === 'users'" (click)="setTab('users')">Users</button>
        <button [class.active]="tab() === 'categories'" (click)="setTab('categories')">Categories</button>
        <button [class.active]="tab() === 'discussions'" (click)="setTab('discussions')">Discussions</button>
      </nav>

      @if (loading()) { <div class="admin-loading"><span></span> Loading this section…</div> }
      @else if (tab() === 'overview') {
        <section class="admin-panel overview-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">YOUR NEXT ACTIONS</span><h2>Keep things moving.</h2></div><button class="admin-button" (click)="setTab('courses')">Review courses <span>↗</span></button></div>
          @if (pendingCourses().length) { <p class="admin-muted">There {{ pendingCourses().length === 1 ? 'is' : 'are' }} {{ pendingCourses().length }} course{{ pendingCourses().length === 1 ? '' : 's' }} waiting for review.</p>
            @for (course of pendingCourses().slice(0, 4); track course.id) { <article class="admin-course-row"><div><span class="admin-status">Pending review</span><h3>{{ course.title }}</h3><p>{{ course.summary }}</p></div><button class="admin-row-button" (click)="setTab('courses')">Open review</button></article> }
          } @else { <div class="admin-empty"><b>Your review queue is clear.</b><span>New course submissions will appear here.</span></div> }
        </section>
      } @else if (tab() === 'analytics') {
        <section class="admin-panel analytics-panel">
          <div class="admin-panel-heading"><div><span class="admin-eyebrow">PLATFORM PERFORMANCE</span><h2>Learning at a glance</h2></div>
            <label class="analytics-range">Reporting period<select [value]="analyticsDays()" (change)="changeAnalyticsRange($any($event.target).value)"><option value="7">Last 7 days</option><option value="30">Last 30 days</option><option value="90">Last 90 days</option><option value="365">Last 12 months</option></select></label>
          </div>
          @if (analytics(); as data) {
            <p class="analytics-updated">Updated {{ data.generatedAtUtc | date:'medium' }} · Trends show the last 12 calendar months.</p>
            <div class="analytics-metric-grid">
              <article><span>ACTIVE ACCOUNTS</span><b>{{ data.activeUsers }}</b><small>{{ data.students }} students · {{ data.teachers }} teachers</small></article>
              <article><span>COURSES PUBLISHED</span><b>{{ data.publishedCourses }}</b><small>{{ data.pendingCourses }} waiting for review</small></article>
              <article><span>TOTAL ENROLLMENTS</span><b>{{ data.totalEnrollments }}</b><small>{{ data.enrollmentsInRange }} in selected period</small></article>
              <article><span>LESSON COMPLETION</span><b>{{ data.completionPercentage }}%</b><small>Across enrolled learners</small></article>
              <article><span>NEW ACCOUNTS</span><b>{{ data.newUsersInRange }}</b><small>Last {{ data.rangeDays }} days</small></article>
              <article><span>ASSIGNMENT SUBMISSIONS</span><b>{{ data.totalSubmissions }}</b><small>{{ data.submissionsInRange }} in selected period</small></article>
              <article><span>SUBMISSIONS GRADED</span><b>{{ data.gradedSubmissions }}</b><small>{{ data.totalSubmissions ? (data.gradedSubmissions / data.totalSubmissions * 100 | number:'1.0-0') : 0 }}% of all work</small></article>
              <article><span>AVERAGE GRADE</span><b>{{ data.averageGrade === null ? '—' : (data.averageGrade | number:'1.0-1') }}</b><small>Points across graded work</small></article>
            </div>
            <section class="analytics-chart-panel"><div class="analytics-section-heading"><div><span class="admin-eyebrow">ACTIVITY TREND</span><h3>Community growth</h3></div><div class="analytics-legend"><span><i class="legend-users"></i>Accounts</span><span><i class="legend-enrollments"></i>Enrollments</span><span><i class="legend-submissions"></i>Submissions</span></div></div>
              <div class="analytics-chart" role="img" aria-label="Monthly account, enrollment and assignment submission activity for the last 12 months">
                @for (month of data.monthlyActivity; track month.monthStartUtc) { <div class="analytics-month"><div class="analytics-bars"><i class="bar-users" [style.height.%]="barHeight(month.newUsers)" [title]="month.newUsers + ' new accounts'"></i><i class="bar-enrollments" [style.height.%]="barHeight(month.enrollments)" [title]="month.enrollments + ' enrollments'"></i><i class="bar-submissions" [style.height.%]="barHeight(month.submissions)" [title]="month.submissions + ' submissions'"></i></div><small>{{ month.monthStartUtc | date:'MMM' }}</small></div> }
              </div>
            </section>
            <section class="analytics-course-panel"><div class="analytics-section-heading"><div><span class="admin-eyebrow">COURSE ENGAGEMENT</span><h3>Most enrolled courses</h3></div><span class="admin-count">{{ data.totalCourses }} courses · {{ data.archivedCourses }} archived</span></div>
              @if (!data.topCourses.length) { <div class="admin-empty"><b>No courses to compare yet.</b><span>Course activity will appear here as learners enroll.</span></div> }
              @else { <div class="analytics-course-list">@for (course of data.topCourses; track course.courseId) { <article><div><b>{{ course.title }}</b><small>{{ course.status }} · {{ course.enrollments }} learners</small></div><div class="analytics-course-progress"><span><i [style.width.%]="course.completionPercentage"></i></span><b>{{ course.completionPercentage }}%</b></div></article> }</div> }
            </section>
          }
        </section>
      } @else if (tab() === 'courses') {
        <section class="admin-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">COURSE QUALITY</span><h2>Manage courses</h2></div><span class="admin-count">{{ pendingCourses().length }} waiting for review</span></div>
          @if (editingCourse(); as selectedCourse) {
            <form class="admin-course-editor" (ngSubmit)="saveCourseEdit()">
              <div class="admin-course-editor-heading"><div><span class="admin-eyebrow">EDIT COURSE · {{ selectedCourse.status }}</span><h3>{{ selectedCourse.title }}</h3></div><button class="admin-row-button" type="button" (click)="cancelCourseEdit()">Cancel</button></div>
              <div class="admin-course-editor-fields">
                <label>Course name<input name="courseTitle" required maxlength="150" [(ngModel)]="courseDraft.title"></label>
                <label>Category<select name="courseCategory" required [(ngModel)]="courseDraft.categoryId"><option value="" disabled>Select a category</option>@for (category of categories(); track category.id) { <option [value]="category.id">{{ category.name }}{{ category.isActive ? '' : ' · Inactive' }}</option> }</select></label>
                <label>Duration<input name="courseDuration" required maxlength="100" [(ngModel)]="courseDraft.duration"></label>
                <label>Price (GEL)<input name="coursePrice" type="number" min="0" max="99999999.99" step="0.01" required [(ngModel)]="courseDraft.price"></label>
                <label>Difficulty<select name="courseDifficulty" required [(ngModel)]="courseDraft.difficulty"><option value="Beginner">Beginner</option><option value="Intermediate">Intermediate</option><option value="Advanced">Advanced</option></select></label>
                <label class="wide-field">Prerequisites (optional)<textarea name="coursePrerequisites" maxlength="2000" rows="2" [(ngModel)]="courseDraft.prerequisites"></textarea></label>
                <label class="wide-field">Short description<input name="courseSummary" required maxlength="300" [(ngModel)]="courseDraft.summary"></label>
                <label class="wide-field">Full description<textarea name="courseDescription" required maxlength="10000" rows="5" [(ngModel)]="courseDraft.description"></textarea></label>
              </div>
              <div class="admin-course-image-editor">
                <div class="admin-course-image-preview">@if (courseImagePreview() || selectedCourse.imageUrl) { <img [src]="courseImagePreview() || selectedCourse.imageUrl" [alt]="courseDraft.title + ' cover'"> } @else { <span>No cover photo</span> }</div>
                <div class="admin-course-image-control"><label>Course cover photo<input name="courseImage" type="file" accept="image/jpeg,image/png,image/webp" (change)="selectCourseImage($event)"></label><p>Choose a JPEG, PNG or WebP image up to 8 MB. The new photo uploads to Cloudinary when you save.</p>@if (selectedCourseImageName()) { <small>Selected: {{ selectedCourseImageName() }}</small> }</div>
              </div>
              <div class="admin-review-actions"><button class="admin-button" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Save course changes' }} <span>↗</span></button><button class="admin-row-button" type="button" [disabled]="busy()" (click)="cancelCourseEdit()">Cancel</button></div>
            </form>
          }
          @if (!pendingCourses().length) { <div class="admin-empty"><b>No courses need review.</b><span>When a teacher submits a course, it will be listed here.</span></div> }
          @for (course of pendingCourses(); track course.id) { <article class="admin-review-card"><div class="admin-course-copy"><span class="admin-status">Pending review</span><h3>{{ course.title }}</h3><p class="admin-course-summary">{{ course.summary }}</p><p>{{ course.description }}</p><time>Submitted {{ course.updatedAtUtc | date:'medium' }}</time></div><label class="admin-rejection-label">Notes for the teacher (required to reject)<textarea rows="3" maxlength="1000" [value]="rejectionReason(course.id)" (input)="setRejectionReason(course.id, $any($event.target).value)" placeholder="Explain what should be revised"></textarea></label><div class="admin-review-actions"><button class="admin-button" [disabled]="busy()" (click)="approveCourse(course)">Approve and publish <span>✓</span></button><button class="admin-row-button danger" [disabled]="busy() || !rejectionReason(course.id).trim()" (click)="rejectCourse(course)">Request changes</button></div></article> }
          <div class="admin-managed-courses-heading"><div><span class="admin-eyebrow">FULL CATALOGUE</span><h3>All courses</h3></div><label class="admin-course-search"><span class="visually-hidden">Search courses</span><input name="courseSearch" [(ngModel)]="courseSearch" placeholder="Search courses"></label></div>
          @if (!allCourses().length) { <div class="admin-empty"><b>No courses have been created yet.</b><span>Course drafts and published courses will appear here.</span></div> }
          @else if (!filteredCourses().length) { <div class="admin-empty"><b>No matching courses.</b><span>Try another course name or description.</span></div> }
          <div class="admin-managed-course-list">@for (course of filteredCourses(); track course.id) { <article class="admin-managed-course">
            @if (course.imageUrl) { <img class="admin-managed-course-image" [src]="course.imageUrl" [alt]="course.title + ' cover'" loading="lazy"> } @else { <div class="admin-managed-course-image empty-cover">N</div> }
            <div class="admin-managed-course-copy"><span class="admin-status">{{ course.status }}</span><h3>{{ course.title }}</h3><p>{{ course.summary }}</p><small>{{ categoryName(course.categoryId) }} · Updated {{ course.updatedAtUtc | date:'mediumDate' }}</small></div>
            <button class="admin-row-button" [disabled]="busy()" (click)="editCourse(course)">Edit course</button>
          </article> }</div>
        </section>
      } @else if (tab() === 'users') {
        <section class="admin-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">COMMUNITY ACCESS</span><h2>Manage users</h2></div><span class="admin-count">{{ userTotal() }} total</span></div>
          <form class="admin-search" (ngSubmit)="searchUsers()"><label class="visually-hidden" for="user-search">Search users</label><input id="user-search" name="userSearch" [(ngModel)]="userSearch" placeholder="Search name or email" maxlength="100"><button class="admin-button" [disabled]="loading()">Search</button></form>
          @if (!users().length) { <div class="admin-empty"><b>No users found.</b><span>Try another name or email address.</span></div> }
          <div class="admin-user-list">@for (user of users(); track user.id) { <article class="admin-user-row"><div class="admin-user-identity"><span class="admin-avatar">{{ initials(user) }}</span><div><b>{{ user.firstName }} {{ user.lastName }}</b><span>{{ user.email }}</span></div></div><span class="admin-user-state" [class.inactive]="!user.isActive">{{ user.isActive ? 'Active' : 'Inactive' }}{{ user.mustChangePassword ? ' · Password update required' : '' }}</span><label class="visually-hidden" [for]="'role-' + user.id">Role for {{ user.email }}</label><select [id]="'role-' + user.id" [value]="user.roles[0]" (change)="changeRole(user, $any($event.target).value)" [disabled]="busy()"><option>Student</option><option>Teacher</option><option>Admin</option></select><button class="admin-row-button" [class.danger]="user.isActive" [disabled]="busy()" (click)="changeStatus(user)">{{ user.isActive ? 'Deactivate' : 'Activate' }}</button><button class="admin-row-button danger" [disabled]="busy()" (click)="removeUser(user)">Remove</button></article> }</div>
          @if (userTotal() > users().length) { <p class="admin-muted">Showing {{ users().length }} users of {{ userTotal() }}. Refine the search to find a specific account.</p> }
        </section>
      } @else if (tab() === 'categories') {
        <section class="admin-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">COURSE SUBJECTS</span><h2>Manage categories</h2></div><span class="admin-count">{{ categories().length }} categories</span></div>
          <form class="admin-category-form" (ngSubmit)="saveCategory()"><h3>{{ editingCategoryId() ? 'Edit category' : 'Add a category' }}</h3><div class="admin-category-fields"><label>Category name<input name="categoryName" required maxlength="100" [(ngModel)]="categoryDraft.name" placeholder="For example, Mathematics"></label><label>Description<input name="categoryDescription" maxlength="500" [(ngModel)]="categoryDraft.description" placeholder="A short description (optional)"></label></div><div class="admin-review-actions"><button class="admin-button" [disabled]="busy()">{{ editingCategoryId() ? 'Save changes' : 'Create category' }} <span>↗</span></button>@if (editingCategoryId()) { <button class="admin-row-button" type="button" (click)="cancelCategoryEdit()">Cancel</button> }</div></form>
          @if (!categories().length) { <div class="admin-empty"><b>No categories yet.</b><span>Create one so teachers can organize their courses.</span></div> }
          @for (category of categories(); track category.id) { <article class="admin-category-row"><div><b>{{ category.name }}</b><p>{{ category.description || 'No description' }}</p></div><span class="admin-user-state" [class.inactive]="!category.isActive">{{ category.isActive ? 'Active' : 'Inactive' }}</span><button class="admin-row-button" (click)="editCategory(category)">Edit</button><button class="admin-row-button" [class.danger]="category.isActive" [disabled]="busy()" (click)="toggleCategory(category)">{{ category.isActive ? 'Deactivate' : 'Activate' }}</button></article> }
        </section>
      } @else {
        <section class="admin-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">COMMUNITY CARE</span><h2>Discussion moderation</h2></div></div>
          <label class="admin-course-select">Course<select name="moderationCourse" [value]="discussionCourseId()" (change)="selectDiscussionCourse($any($event.target).value)"><option value="">Choose a course</option>@for (course of allCourses(); track course.id) { <option [value]="course.id">{{ course.title }}</option> }</select></label>
          @if (discussionCourseId() && discussions().length < discussionTotalCount()) { <p class="admin-muted">Showing {{ discussions().length }} of {{ discussionTotalCount() }} posts. <button class="admin-row-button" [disabled]="loadingOlderDiscussions()" (click)="loadOlderDiscussions()">{{ loadingOlderDiscussions() ? 'Loading…' : 'Load older posts' }}</button></p> }
          @if (discussionCourseId() && !discussions().length) { <div class="admin-empty"><b>No posts in this course.</b><span>Discussion posts will appear here when learners start talking.</span></div> }
          @for (post of discussions(); track post.id) { <article class="admin-discussion" [class.hidden-post]="post.isHidden"><div class="admin-discussion-meta"><span class="admin-status" [class.hidden-status]="post.isHidden">{{ post.isHidden ? 'Hidden' : 'Visible' }}{{ post.parentPostId ? ' · Reply' : ' · Post' }}</span><time>{{ post.createdAtUtc | date:'medium' }}</time></div><p>{{ post.body }}</p>@if (post.moderationReason) { <small>Reason: {{ post.moderationReason }}</small> }@if (!post.isHidden) { <label class="admin-rejection-label">Reason for hiding<textarea rows="2" [value]="moderationReason(post.id)" (input)="setModerationReason(post.id, $any($event.target).value)" placeholder="A brief explanation"></textarea></label> }<button class="admin-row-button" [class.danger]="!post.isHidden" [disabled]="busy() || (!post.isHidden && !moderationReason(post.id).trim())" (click)="moderatePost(post)">{{ post.isHidden ? 'Restore post' : 'Hide post' }}</button></article> }
        </section>
      }
    </main>
  `,
})
export class AdminDashboardPage implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  readonly tab = signal<AdminTab>('overview');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly pendingCourses = signal<Course[]>([]);
  readonly allCourses = signal<Course[]>([]);
  readonly editingCourse = signal<Course | null>(null);
  readonly courseImagePreview = signal<string | null>(null);
  readonly users = signal<AdminUserSummary[]>([]);
  readonly userTotal = signal(0);
  readonly categories = signal<Category[]>([]);
  readonly analytics = signal<AdminAnalytics | null>(null);
  readonly analyticsDays = signal(30);
  readonly discussions = signal<DiscussionPost[]>([]);
  readonly discussionPage = signal(1);
  readonly discussionTotalCount = signal(0);
  readonly loadingOlderDiscussions = signal(false);
  readonly discussionCourseId = signal('');
  readonly editingCategoryId = signal<string | null>(null);
  readonly courseDraft: { categoryId: string; title: string; summary: string; description: string; duration: string; price: number; difficulty: Course['difficulty']; prerequisites: string } =
    { categoryId: '', title: '', summary: '', description: '', duration: 'Self-paced', price: 0, difficulty: 'Beginner', prerequisites: '' };
  readonly categoryDraft = { name: '', description: '' };
  userSearch = '';
  courseSearch = '';
  private selectedCourseImage: File | null = null;
  private courseImageObjectUrl: string | null = null;
  private readonly rejectionReasons = new Map<string, string>();
  private readonly moderationReasons = new Map<string, string>();

  async ngOnInit(): Promise<void> { await this.setTab('overview'); }
  ngOnDestroy(): void { this.releaseCourseImagePreview(); }

  activeCategoryCount(): number { return this.categories().filter(category => category.isActive).length; }
  barHeight(value: number): number {
    const max = Math.max(1, ...(this.analytics()?.monthlyActivity.flatMap(month => [month.newUsers, month.enrollments, month.submissions]) ?? []));
    return value === 0 ? 2 : Math.max(7, (value / max) * 100);
  }
  initials(user: AdminUserSummary): string { return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase() || 'U'; }
  rejectionReason(id: string): string { return this.rejectionReasons.get(id) ?? ''; }
  setRejectionReason(id: string, value: string): void { this.rejectionReasons.set(id, value); }
  moderationReason(id: string): string { return this.moderationReasons.get(id) ?? ''; }
  setModerationReason(id: string, value: string): void { this.moderationReasons.set(id, value); }
  categoryName(id: string): string { return this.categories().find(category => category.id === id)?.name ?? 'Uncategorized'; }
  filteredCourses(): Course[] {
    const term = this.courseSearch.trim().toLocaleLowerCase();
    if (!term) return this.allCourses();
    return this.allCourses().filter(course =>
      `${course.title} ${course.summary} ${course.description}`.toLocaleLowerCase().includes(term));
  }
  selectedCourseImageName(): string { return this.selectedCourseImage?.name ?? ''; }

  editCourse(course: Course): void {
    this.releaseCourseImagePreview();
    this.selectedCourseImage = null;
    this.editingCourse.set(course);
    this.courseDraft.categoryId = course.categoryId;
    this.courseDraft.title = course.title;
    this.courseDraft.summary = course.summary;
    this.courseDraft.description = course.description;
    this.courseDraft.duration = course.duration;
    this.courseDraft.price = course.price;
    this.courseDraft.difficulty = course.difficulty;
    this.courseDraft.prerequisites = course.prerequisites ?? '';
    this.error.set('');
    this.notice.set('');
  }

  cancelCourseEdit(): void {
    this.releaseCourseImagePreview();
    this.selectedCourseImage = null;
    this.editingCourse.set(null);
    this.courseDraft.categoryId = '';
    this.courseDraft.title = '';
    this.courseDraft.summary = '';
    this.courseDraft.description = '';
    this.courseDraft.duration = 'Self-paced';
    this.courseDraft.price = 0;
    this.courseDraft.difficulty = 'Beginner';
    this.courseDraft.prerequisites = '';
  }

  selectCourseImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.error.set('Choose a JPEG, PNG or WebP image.');
      input.value = '';
      return;
    }
    if (file.size > 8 * 1024 * 1024) {
      this.error.set('Course images must be 8 MB or smaller.');
      input.value = '';
      return;
    }
    this.error.set('');
    this.releaseCourseImagePreview();
    this.selectedCourseImage = file;
    this.courseImageObjectUrl = URL.createObjectURL(file);
    this.courseImagePreview.set(this.courseImageObjectUrl);
  }

  async saveCourseEdit(): Promise<void> {
    const course = this.editingCourse();
    if (!course) return;
    const image = this.selectedCourseImage;
    await this.perform(async () => {
      const updated = await firstValueFrom(this.api.adminUpdateCourse(course.id, this.courseDraft, image));
      this.allCourses.update(list => list.map(item => item.id === updated.id ? updated : item));
      this.pendingCourses.update(list => list.map(item => item.id === updated.id ? updated : item));
      this.cancelCourseEdit();
      return `“${updated.title}” was updated${image ? ' and its photo was uploaded to Cloudinary' : ''}.`;
    });
  }

  async setTab(tab: AdminTab): Promise<void> {
    this.tab.set(tab); this.error.set(''); this.notice.set(''); this.loading.set(true);
    try {
      if (tab === 'overview') await this.loadOverview();
      if (tab === 'analytics') await this.loadAnalytics();
      if (tab === 'courses') await this.loadCourses();
      if (tab === 'users') await this.loadUsers();
      if (tab === 'categories') await this.loadCategories();
      if (tab === 'discussions') await this.loadCourses();
    } catch (err) { this.error.set(apiMessage(err, 'This section could not be loaded. Please try again.')); }
    finally { this.loading.set(false); }
  }

  async searchUsers(): Promise<void> { this.loading.set(true); this.error.set(''); try { await this.loadUsers(); } catch (err) { this.error.set(apiMessage(err, 'Users could not be loaded.')); } finally { this.loading.set(false); } }
  async changeAnalyticsRange(value: string): Promise<void> {
    const days = Number(value);
    if (![7, 30, 90, 365].includes(days)) return;
    this.analyticsDays.set(days);
    this.loading.set(true); this.error.set('');
    try { await this.loadAnalytics(); }
    catch (err) { this.error.set(apiMessage(err, 'Analytics could not be loaded.')); }
    finally { this.loading.set(false); }
  }
  async selectDiscussionCourse(id: string): Promise<void> {
    this.discussionCourseId.set(id); this.discussions.set([]); this.discussionPage.set(1); this.discussionTotalCount.set(0); this.error.set('');
    if (!id) return;
    this.loading.set(true);
    try {
      const page = await firstValueFrom(this.api.adminCourseDiscussions(id));
      this.discussions.set([...page.items].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
      this.discussionPage.set(page.page); this.discussionTotalCount.set(page.totalCount);
    }
    catch (err) { this.error.set(apiMessage(err, 'Discussion posts could not be loaded.')); }
    finally { this.loading.set(false); }
  }

  async loadOlderDiscussions(): Promise<void> {
    const courseId = this.discussionCourseId();
    if (!courseId || this.loadingOlderDiscussions() || this.discussions().length >= this.discussionTotalCount()) return;
    this.loadingOlderDiscussions.set(true); this.error.set('');
    try {
      const page = await firstValueFrom(this.api.adminCourseDiscussions(courseId, this.discussionPage() + 1));
      const merged = new Map([...this.discussions(), ...page.items].map(post => [post.id, post]));
      this.discussions.set([...merged.values()].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
      this.discussionPage.set(page.page); this.discussionTotalCount.set(page.totalCount);
    } catch (err) { this.error.set(apiMessage(err, 'Older discussion posts could not be loaded.')); }
    finally { this.loadingOlderDiscussions.set(false); }
  }

  async approveCourse(course: Course): Promise<void> {
    await this.perform(async () => { await firstValueFrom(this.api.adminApproveCourse(course.id)); this.pendingCourses.update(list => list.filter(item => item.id !== course.id)); this.allCourses.update(list => list.map(item => item.id === course.id ? { ...item, status: 'Published' } : item)); return `“${course.title}” is now published.`; });
  }
  async rejectCourse(course: Course): Promise<void> {
    const reason = this.rejectionReason(course.id).trim();
    if (!reason) return;
    await this.perform(async () => { await firstValueFrom(this.api.adminRejectCourse(course.id, reason)); this.pendingCourses.update(list => list.filter(item => item.id !== course.id)); this.allCourses.update(list => list.map(item => item.id === course.id ? { ...item, status: 'Rejected', rejectionReason: reason } : item)); return `Review notes sent for “${course.title}”.`; });
  }
  async changeRole(user: AdminUserSummary, role: string): Promise<void> {
    if (role === user.roles[0]) return;
    await this.perform(async () => { await firstValueFrom(this.api.adminChangeUserRole(user.id, role)); this.users.update(list => list.map(item => item.id === user.id ? { ...item, roles: [role] } : item)); return `${user.email} is now assigned the ${role} role.`; });
  }
  async changeStatus(user: AdminUserSummary): Promise<void> {
    const isActive = !user.isActive;
    await this.perform(async () => { await firstValueFrom(this.api.adminSetUserStatus(user.id, isActive)); this.users.update(list => list.map(item => item.id === user.id ? { ...item, isActive } : item)); return `${user.email} is ${isActive ? 'active' : 'deactivated'}.`; });
  }
  async removeUser(user: AdminUserSummary): Promise<void> {
    const confirmed = window.confirm(
      `Remove ${user.email}? Their sign-in will be disabled and their personal account details anonymized. Their courses and learning history will be kept. This cannot be undone.`,
    );
    if (!confirmed) return;
    await this.perform(async () => {
      await firstValueFrom(this.api.adminRemoveUser(user.id));
      this.users.update(list => list.filter(item => item.id !== user.id));
      this.userTotal.update(total => Math.max(0, total - 1));
      return `${user.email} was removed. Their courses and learning history were kept.`;
    });
  }
  editCategory(category: Category): void { this.editingCategoryId.set(category.id); this.categoryDraft.name = category.name; this.categoryDraft.description = category.description ?? ''; }
  cancelCategoryEdit(): void { this.editingCategoryId.set(null); this.categoryDraft.name = ''; this.categoryDraft.description = ''; }
  async saveCategory(): Promise<void> {
    const id = this.editingCategoryId();
    await this.perform(async () => {
      const saved = id
        ? await firstValueFrom(this.api.adminUpdateCategory(id, this.categoryDraft.name.trim(), this.categoryDraft.description.trim()))
        : await firstValueFrom(this.api.adminCreateCategory(this.categoryDraft.name.trim(), this.categoryDraft.description.trim()));
      this.categories.update(list => id ? list.map(item => item.id === id ? saved : item) : [...list, saved]);
      this.cancelCategoryEdit();
      return id ? 'Category updated.' : 'Category created.';
    });
  }
  async toggleCategory(category: Category): Promise<void> {
    const isActive = !category.isActive;
    await this.perform(async () => { const saved = await firstValueFrom(this.api.adminSetCategoryStatus(category.id, isActive)); this.categories.update(list => list.map(item => item.id === category.id ? saved : item)); return `${category.name} is ${isActive ? 'active' : 'inactive'}.`; });
  }
  async moderatePost(post: DiscussionPost): Promise<void> {
    const isHidden = !post.isHidden;
    const reason = isHidden ? this.moderationReason(post.id).trim() : '';
    if (isHidden && !reason) return;
    await this.perform(async () => { const updated = await firstValueFrom(this.api.adminModerateDiscussion(post.id, isHidden, reason)); this.discussions.update(list => list.map(item => item.id === post.id ? updated : item)); this.moderationReasons.delete(post.id); return `Discussion ${isHidden ? 'hidden' : 'restored'}.`; });
  }

  private async loadOverview(): Promise<void> {
    const [courses, pending, userPage, categories] = await Promise.all([
      firstValueFrom(this.api.adminCourses()), firstValueFrom(this.api.adminPendingCourses()),
      firstValueFrom(this.api.adminUsers('', 1, 1)), firstValueFrom(this.api.adminCategories()),
    ]);
    this.allCourses.set(courses); this.pendingCourses.set(pending); this.userTotal.set(userPage.totalCount); this.categories.set(categories);
  }
  private async loadCourses(): Promise<void> {
    const [all, pending, categories] = await Promise.all([
      firstValueFrom(this.api.adminCourses()),
      firstValueFrom(this.api.adminPendingCourses()),
      firstValueFrom(this.api.adminCategories()),
    ]);
    this.allCourses.set(all); this.pendingCourses.set(pending); this.categories.set(categories);
  }
  private async loadUsers(): Promise<void> {
    const response = await firstValueFrom(this.api.adminUsers(this.userSearch, 1, 100));
    this.users.set(response.users); this.userTotal.set(response.totalCount);
  }
  private async loadCategories(): Promise<void> { this.categories.set(await firstValueFrom(this.api.adminCategories())); }
  private async loadAnalytics(): Promise<void> {
    this.analytics.set(await firstValueFrom(this.api.adminAnalytics(this.analyticsDays())));
  }
  private releaseCourseImagePreview(): void {
    if (this.courseImageObjectUrl) URL.revokeObjectURL(this.courseImageObjectUrl);
    this.courseImageObjectUrl = null;
    this.courseImagePreview.set(null);
  }
  private async perform(action: () => Promise<string>): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { this.notice.set(await action()); }
    catch (err) { this.error.set(apiMessage(err, 'That change could not be saved.')); }
    finally { this.busy.set(false); }
  }
}
