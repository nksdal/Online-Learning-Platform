import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AdminUserSummary, Category, Course, DiscussionPost } from '../core/models';

type AdminTab = 'overview' | 'courses' | 'users' | 'categories' | 'discussions';

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
        <button [class.active]="tab() === 'courses'" (click)="setTab('courses')">Course review <span>{{ pendingCourses().length }}</span></button>
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
      } @else if (tab() === 'courses') {
        <section class="admin-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">COURSE QUALITY</span><h2>Review submissions</h2></div><span class="admin-count">{{ pendingCourses().length }} waiting</span></div>
          @if (!pendingCourses().length) { <div class="admin-empty"><b>No courses need review.</b><span>When a teacher submits a course, it will be listed here.</span></div> }
          @for (course of pendingCourses(); track course.id) { <article class="admin-review-card"><div class="admin-course-copy"><span class="admin-status">Pending review</span><h3>{{ course.title }}</h3><p class="admin-course-summary">{{ course.summary }}</p><p>{{ course.description }}</p><time>Submitted {{ course.updatedAtUtc | date:'medium' }}</time></div><label class="admin-rejection-label">Notes for the teacher (required to reject)<textarea rows="3" maxlength="1000" [value]="rejectionReason(course.id)" (input)="setRejectionReason(course.id, $any($event.target).value)" placeholder="Explain what should be revised"></textarea></label><div class="admin-review-actions"><button class="admin-button" [disabled]="busy()" (click)="approveCourse(course)">Approve and publish <span>✓</span></button><button class="admin-row-button danger" [disabled]="busy() || !rejectionReason(course.id).trim()" (click)="rejectCourse(course)">Request changes</button></div></article> }
        </section>
      } @else if (tab() === 'users') {
        <section class="admin-panel"><div class="admin-panel-heading"><div><span class="admin-eyebrow">COMMUNITY ACCESS</span><h2>Manage users</h2></div><span class="admin-count">{{ userTotal() }} total</span></div>
          <form class="admin-search" (ngSubmit)="searchUsers()"><label class="visually-hidden" for="user-search">Search users</label><input id="user-search" name="userSearch" [(ngModel)]="userSearch" placeholder="Search name or email" maxlength="100"><button class="admin-button" [disabled]="loading()">Search</button></form>
          @if (!users().length) { <div class="admin-empty"><b>No users found.</b><span>Try another name or email address.</span></div> }
          <div class="admin-user-list">@for (user of users(); track user.id) { <article class="admin-user-row"><div class="admin-user-identity"><span class="admin-avatar">{{ initials(user) }}</span><div><b>{{ user.firstName }} {{ user.lastName }}</b><span>{{ user.email }}</span></div></div><span class="admin-user-state" [class.inactive]="!user.isActive">{{ user.isActive ? 'Active' : 'Inactive' }}{{ user.mustChangePassword ? ' · Password update required' : '' }}</span><label class="visually-hidden" [for]="'role-' + user.id">Role for {{ user.email }}</label><select [id]="'role-' + user.id" [value]="user.roles[0]" (change)="changeRole(user, $any($event.target).value)" [disabled]="busy()"><option>Student</option><option>Teacher</option><option>Admin</option></select><button class="admin-row-button" [class.danger]="user.isActive" [disabled]="busy()" (click)="changeStatus(user)">{{ user.isActive ? 'Deactivate' : 'Activate' }}</button></article> }</div>
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
          @if (discussionCourseId() && !discussions().length) { <div class="admin-empty"><b>No posts in this course.</b><span>Discussion posts will appear here when learners start talking.</span></div> }
          @for (post of discussions(); track post.id) { <article class="admin-discussion" [class.hidden-post]="post.isHidden"><div class="admin-discussion-meta"><span class="admin-status" [class.hidden-status]="post.isHidden">{{ post.isHidden ? 'Hidden' : 'Visible' }}{{ post.parentPostId ? ' · Reply' : ' · Post' }}</span><time>{{ post.createdAtUtc | date:'medium' }}</time></div><p>{{ post.body }}</p>@if (post.moderationReason) { <small>Reason: {{ post.moderationReason }}</small> }@if (!post.isHidden) { <label class="admin-rejection-label">Reason for hiding<textarea rows="2" [value]="moderationReason(post.id)" (input)="setModerationReason(post.id, $any($event.target).value)" placeholder="A brief explanation"></textarea></label> }<button class="admin-row-button" [class.danger]="!post.isHidden" [disabled]="busy() || (!post.isHidden && !moderationReason(post.id).trim())" (click)="moderatePost(post)">{{ post.isHidden ? 'Restore post' : 'Hide post' }}</button></article> }
        </section>
      }
    </main>
  `,
})
export class AdminDashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  readonly tab = signal<AdminTab>('overview');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly pendingCourses = signal<Course[]>([]);
  readonly allCourses = signal<Course[]>([]);
  readonly users = signal<AdminUserSummary[]>([]);
  readonly userTotal = signal(0);
  readonly categories = signal<Category[]>([]);
  readonly discussions = signal<DiscussionPost[]>([]);
  readonly discussionCourseId = signal('');
  readonly editingCategoryId = signal<string | null>(null);
  readonly categoryDraft = { name: '', description: '' };
  userSearch = '';
  private readonly rejectionReasons = new Map<string, string>();
  private readonly moderationReasons = new Map<string, string>();

  async ngOnInit(): Promise<void> { await this.setTab('overview'); }

  activeCategoryCount(): number { return this.categories().filter(category => category.isActive).length; }
  initials(user: AdminUserSummary): string { return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase() || 'U'; }
  rejectionReason(id: string): string { return this.rejectionReasons.get(id) ?? ''; }
  setRejectionReason(id: string, value: string): void { this.rejectionReasons.set(id, value); }
  moderationReason(id: string): string { return this.moderationReasons.get(id) ?? ''; }
  setModerationReason(id: string, value: string): void { this.moderationReasons.set(id, value); }

  async setTab(tab: AdminTab): Promise<void> {
    this.tab.set(tab); this.error.set(''); this.notice.set(''); this.loading.set(true);
    try {
      if (tab === 'overview') await this.loadOverview();
      if (tab === 'courses') await this.loadCourses();
      if (tab === 'users') await this.loadUsers();
      if (tab === 'categories') await this.loadCategories();
      if (tab === 'discussions') await this.loadCourses();
    } catch (err) { this.error.set(apiMessage(err, 'This section could not be loaded. Please try again.')); }
    finally { this.loading.set(false); }
  }

  async searchUsers(): Promise<void> { this.loading.set(true); this.error.set(''); try { await this.loadUsers(); } catch (err) { this.error.set(apiMessage(err, 'Users could not be loaded.')); } finally { this.loading.set(false); } }
  async selectDiscussionCourse(id: string): Promise<void> {
    this.discussionCourseId.set(id); this.discussions.set([]); this.error.set('');
    if (!id) return;
    this.loading.set(true);
    try { this.discussions.set(await firstValueFrom(this.api.adminCourseDiscussions(id))); }
    catch (err) { this.error.set(apiMessage(err, 'Discussion posts could not be loaded.')); }
    finally { this.loading.set(false); }
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
    const [all, pending] = await Promise.all([firstValueFrom(this.api.adminCourses()), firstValueFrom(this.api.adminPendingCourses())]);
    this.allCourses.set(all); this.pendingCourses.set(pending);
  }
  private async loadUsers(): Promise<void> {
    const response = await firstValueFrom(this.api.adminUsers(this.userSearch, 1, 100));
    this.users.set(response.users); this.userTotal.set(response.totalCount);
  }
  private async loadCategories(): Promise<void> { this.categories.set(await firstValueFrom(this.api.adminCategories())); }
  private async perform(action: () => Promise<string>): Promise<void> {
    this.busy.set(true); this.error.set(''); this.notice.set('');
    try { this.notice.set(await action()); }
    catch (err) { this.error.set(apiMessage(err, 'That change could not be saved.')); }
    finally { this.busy.set(false); }
  }
}
