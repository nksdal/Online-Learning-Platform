import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Assignment, Category, Course, DiscussionPost, Lesson, TeacherAssignmentSubmission } from '../core/models';

type TeacherTab = 'lessons' | 'assignments' | 'submissions' | 'discussion';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="teacher-page">
      <header class="teacher-hero"><div><span class="nik-eyebrow">NIKLADORI · TEACHER STUDIO</span><h1>Make room for<br /><em>good teaching.</em></h1><p>Build a course, guide your learners, and keep everything in one place.</p></div><button class="nik-button nik-button-mint" (click)="startNewCourse()">Create a course <span>＋</span></button><span class="teacher-hero-mark" aria-hidden="true">N</span></header>
      @if (notice()) { <div class="teacher-notice" role="status">{{ notice() }}</div> }
      @if (error()) { <div class="teacher-error" role="alert">{{ error() }} <button (click)="error.set('')" aria-label="Dismiss">×</button></div> }

      <div class="teacher-layout">
        <aside class="teacher-course-rail">
          <div class="teacher-rail-heading"><span class="nik-eyebrow">YOUR COURSES</span><span>{{ courses().length }}</span></div>
          @if (loadingCourses()) { <div class="teacher-rail-empty"><span class="nik-spinner"></span> Loading your courses…</div> }
          @else if (courses().length === 0) { <div class="teacher-rail-empty">Your courses will appear here. Create one to get started.</div> }
          @else { @for (course of courses(); track course.id) { <button class="teacher-course-item" [class.active]="selectedCourse()?.id === course.id" (click)="selectCourse(course)"><span class="teacher-course-item-title">{{ course.title }}</span><span class="teacher-status" [class]="'teacher-status status-' + course.status.toLowerCase()">{{ statusLabel(course.status) }}</span></button> } }
        </aside>

        <section class="teacher-main">
          @if (courseEditorOpen()) {
            <article class="teacher-form-card"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">{{ selectedCourse() ? 'COURSE DETAILS' : 'NEW COURSE' }}</span><h2>{{ selectedCourse() ? 'Edit your course' : 'Start a course' }}</h2></div><button class="teacher-close" type="button" (click)="cancelCourseEdit()" aria-label="Close editor">×</button></div>
              @if (!categories().length) { <p class="teacher-form-help">There are no active categories yet. Ask an administrator to add one before creating a course.</p> }
              <form (ngSubmit)="saveCourse()"><label>Course title<input name="courseTitle" maxlength="150" required [(ngModel)]="courseDraft.title" placeholder="For example, Creative Coding Basics" /></label><label>Subject<select name="courseCategory" required [(ngModel)]="courseDraft.categoryId"><option value="" disabled>Select a category</option>@for (category of categories(); track category.id) { <option [value]="category.id">{{ category.name }}</option> }</select></label><label>Short summary<input name="courseSummary" maxlength="300" required [(ngModel)]="courseDraft.summary" placeholder="What will learners explore?" /></label><label>Course description<textarea name="courseDescription" maxlength="10000" rows="5" required [(ngModel)]="courseDraft.description" placeholder="Explain the course and what learners can expect."></textarea></label><div class="teacher-form-actions"><button class="teacher-secondary-button" type="button" (click)="cancelCourseEdit()">Cancel</button><button class="nik-button nik-button-navy" [disabled]="saving() || !categories().length">{{ saving() ? 'Saving…' : 'Save draft' }} <span>↗</span></button></div></form>
            </article>
          } @else if (selectedCourse(); as course) {
            <header class="teacher-selected-course"><div><span class="nik-eyebrow">COURSE WORKSPACE</span><h2>{{ course.title }}</h2><p>{{ course.summary }}</p></div><div class="teacher-course-actions">
              @if (canEdit(course)) { <button class="teacher-secondary-button" (click)="editCourse(course)">Edit details</button> }
              @if (course.status === 'Draft' || course.status === 'Rejected') { <button class="nik-button nik-button-navy" (click)="submitCourse(course)">{{ course.status === 'Rejected' ? 'Resubmit for review' : 'Submit for review' }} <span>↗</span></button> }
              @if (course.status === 'Published') { <button class="teacher-secondary-button" (click)="archiveCourse(course)">Archive course</button> }
            </div></header>
            @if (course.status === 'Rejected' && course.rejectionReason) { <div class="teacher-rejection"><b>Review notes</b><p>{{ course.rejectionReason }}</p><span>Edit your course and submit it again when you’re ready.</span></div> }
            @if (course.status === 'PendingApproval') { <div class="teacher-info-banner">This course is with the admin team for review. Its details, lessons, and assignments are temporarily read-only.</div> }
            @if (course.status === 'Archived') { <div class="teacher-info-banner">This course is archived and read-only. Existing learner access remains available.</div> }

            <nav class="teacher-tabs" aria-label="Course management"><button [class.active]="activeTab() === 'lessons'" (click)="setTab('lessons')">Lessons <span>{{ lessons().length }}</span></button><button [class.active]="activeTab() === 'assignments'" (click)="setTab('assignments')">Assignments <span>{{ assignments().length }}</span></button><button [class.active]="activeTab() === 'submissions'" (click)="setTab('submissions')">Grading <span>{{ ungradedCount() }}</span></button><button [class.active]="activeTab() === 'discussion'" (click)="setTab('discussion')">Discussion <span>{{ discussions().length }}</span></button></nav>
            @if (loadingWorkspace()) { <div class="nik-state"><span class="nik-spinner"></span> Loading course workspace…</div> }
            @else if (activeTab() === 'lessons') {
              <section class="teacher-tab-panel"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">COURSE CONTENT</span><h3>Lessons</h3></div>@if (canManageContent(course)) { <button class="teacher-secondary-button" (click)="newLesson()">＋ Add lesson</button> }</div>
                @if (!lessons().length) { <div class="teacher-empty"><b>No lessons yet.</b><span>Add the first lesson to give learners a place to begin.</span></div> }
                @for (lesson of lessons(); track lesson.id) { <article class="teacher-list-row"><span class="teacher-order">{{ lesson.position < 10 ? '0' + lesson.position : lesson.position }}</span><div><b>{{ lesson.title }}</b><p>{{ lesson.content | slice:0:110 }}{{ lesson.content.length > 110 ? '…' : '' }}</p></div>@if (canManageContent(course)) { <button class="teacher-row-action" (click)="editLesson(lesson)">Edit</button><button class="teacher-row-action danger" (click)="deleteLesson(lesson)">Delete</button> }</article> }
                @if (lessonEditorOpen()) { <form class="teacher-inline-form" (ngSubmit)="saveLesson()"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">{{ editingLessonId() ? 'EDIT LESSON' : 'NEW LESSON' }}</span><h3>{{ editingLessonId() ? 'Update lesson' : 'Add a lesson' }}</h3></div></div><div class="teacher-form-grid"><label>Title<input name="lessonTitle" required maxlength="150" [(ngModel)]="lessonDraft.title" /></label><label>Position<input name="lessonPosition" type="number" min="1" required [(ngModel)]="lessonDraft.position" /></label></div><label>Lesson content<textarea name="lessonContent" rows="7" maxlength="20000" required [(ngModel)]="lessonDraft.content" placeholder="Write the lesson content here…"></textarea></label><div class="teacher-form-actions"><button class="teacher-secondary-button" type="button" (click)="cancelLessonEdit()">Cancel</button><button class="nik-button nik-button-navy" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save lesson' }}</button></div></form> }
              </section>
            } @else if (activeTab() === 'assignments') {
              <section class="teacher-tab-panel"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">PRACTICE AND APPLY</span><h3>Assignments</h3></div>@if (canManageContent(course)) { <button class="teacher-secondary-button" (click)="newAssignment()">＋ Add assignment</button> }</div>
                @if (!assignments().length) { <div class="teacher-empty"><b>No assignments yet.</b><span>Add a text assignment for learners to practice what they’ve learned.</span></div> }
                @for (assignment of assignments(); track assignment.id) { <article class="teacher-assignment-row"><div><b>{{ assignment.title }}</b><p>{{ assignment.instructions }}</p><span>{{ assignment.maxAttempts }} attempt{{ assignment.maxAttempts === 1 ? '' : 's' }} · Max score {{ assignment.maxScore }}{{ assignment.dueAtUtc ? ' · Due ' + (assignment.dueAtUtc | date:'medium') : ' · No deadline' }}</span></div>@if (canManageContent(course)) { <button class="teacher-row-action" (click)="editAssignment(assignment)">Edit</button> }</article> }
                @if (assignmentEditorOpen()) { <form class="teacher-inline-form" (ngSubmit)="saveAssignment()"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">{{ editingAssignmentId() ? 'EDIT ASSIGNMENT' : 'NEW ASSIGNMENT' }}</span><h3>{{ editingAssignmentId() ? 'Update assignment' : 'Create an assignment' }}</h3></div></div><label>Title<input name="assignmentTitle" required maxlength="150" [(ngModel)]="assignmentDraft.title" /></label><label>Instructions<textarea name="assignmentInstructions" rows="4" required maxlength="10000" [(ngModel)]="assignmentDraft.instructions"></textarea></label><div class="teacher-form-grid"><label>Deadline (optional)<input name="assignmentDue" type="datetime-local" [(ngModel)]="assignmentDraft.dueAtLocal" /></label><label>Maximum attempts<input name="assignmentAttempts" type="number" min="1" max="20" required [(ngModel)]="assignmentDraft.maxAttempts" /></label><label>Maximum score<input name="assignmentMaxScore" type="number" min="0.01" step="0.01" required [(ngModel)]="assignmentDraft.maxScore" /></label></div><div class="teacher-form-actions"><button class="teacher-secondary-button" type="button" (click)="cancelAssignmentEdit()">Cancel</button><button class="nik-button nik-button-navy" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save assignment' }}</button></div></form> }
              </section>
            } @else if (activeTab() === 'submissions') {
              <section class="teacher-tab-panel"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">LEARNER WORK</span><h3>Submission review</h3></div><span class="teacher-review-count">{{ ungradedCount() }} waiting for feedback</span></div>
                @if (!assignments().length) { <div class="teacher-empty"><b>No assignments yet.</b><span>Create an assignment first. Learner submissions will appear here.</span></div> }
                @else if (!allSubmissions().length) { <div class="teacher-empty"><b>No submissions yet.</b><span>When learners submit work, their attempts will appear here for grading.</span></div> }
                @for (submission of allSubmissions(); track submission.id) { <article class="teacher-submission"><div class="teacher-submission-heading"><div><span class="nik-eyebrow">{{ assignmentTitle(submission.assignmentId) }}</span><h4>Attempt {{ submission.attemptNumber }}</h4></div><span class="teacher-grade-pill" [class.graded]="submission.score !== null && submission.score !== undefined">{{ submission.score !== null && submission.score !== undefined ? 'Graded · ' + submission.score + ' / ' + maxScore(submission.assignmentId) : 'Needs review' }}</span></div><p class="teacher-submission-text">{{ submission.text }}</p><time>Submitted {{ submission.submittedAtUtc | date:'medium' }}</time>@if (submission.score !== null && submission.score !== undefined) { <div class="teacher-existing-feedback"><b>Feedback sent</b><p>{{ submission.feedback || 'No written feedback.' }}</p></div> }<div class="teacher-grade-form"><label>Score<input type="number" min="0" [max]="maxScore(submission.assignmentId)" step="0.01" [value]="gradeScore(submission)" (input)="setGradeScore(submission.id, $any($event.target).value)" /></label><label>Feedback<textarea rows="2" maxlength="2000" [value]="gradeFeedback(submission)" (input)="setGradeFeedback(submission.id, $any($event.target).value)" placeholder="Give the learner useful, specific feedback…"></textarea></label><button class="nik-button nik-button-navy" [disabled]="saving()" (click)="gradeSubmission(submission)">{{ saving() ? 'Saving…' : submission.score !== null && submission.score !== undefined ? 'Update grade' : 'Send feedback' }} <span>↗</span></button></div></article> }
              </section>
            } @else {
              <section class="teacher-tab-panel"><div class="teacher-panel-heading"><div><span class="nik-eyebrow">COURSE COMMUNITY</span><h3>Discussion moderation</h3></div><span class="teacher-review-count">{{ hiddenDiscussionCount() }} hidden</span></div>
                @if (!discussions().length) { <div class="teacher-empty"><b>No discussion posts yet.</b><span>Learner questions and replies will appear here.</span></div> }
                @for (post of discussions(); track post.id) { <article class="teacher-discussion" [class.hidden-post]="post.isHidden"><div class="teacher-discussion-head"><span class="teacher-status" [class.status-rejected]="post.isHidden">{{ post.isHidden ? 'Hidden' : 'Visible' }}{{ post.parentPostId ? ' · Reply' : ' · Post' }}</span><time>{{ post.createdAtUtc | date:'medium' }}</time></div><p>{{ post.body }}</p>@if (post.moderationReason) { <small>Moderation reason: {{ post.moderationReason }}</small> }@if (!post.isHidden) { <label>Reason for hiding<textarea rows="2" [value]="moderationReason(post.id)" (input)="setModerationReason(post.id, $any($event.target).value)" placeholder="Explain why this post needs to be hidden"></textarea></label> }<button class="teacher-row-action" [class.danger]="!post.isHidden" [disabled]="saving() || (!post.isHidden && !moderationReason(post.id).trim())" (click)="moderatePost(post)">{{ post.isHidden ? 'Restore post' : 'Hide post' }}</button></article> }
              </section>
            }
          } @else {
            <section class="teacher-empty-state"><span class="teacher-empty-icon">✳</span><span class="nik-eyebrow">YOUR TEACHER WORKSPACE</span><h2>Let’s build something worth learning.</h2><p>Create your first course or choose one from the list to manage lessons, assignments, feedback, and discussion.</p><button class="nik-button nik-button-navy" (click)="startNewCourse()">Create a course <span>＋</span></button></section>
          }
        </section>
      </div>
    </main>
  `,
})
export class TeacherDashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  readonly courses = signal<Course[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly selectedCourse = signal<Course | null>(null);
  readonly lessons = signal<Lesson[]>([]);
  readonly assignments = signal<Assignment[]>([]);
  readonly submissions = signal<TeacherAssignmentSubmission[]>([]);
  readonly discussions = signal<DiscussionPost[]>([]);
  readonly activeTab = signal<TeacherTab>('lessons');
  readonly loadingCourses = signal(true);
  readonly loadingWorkspace = signal(false);
  readonly courseEditorOpen = signal(false);
  readonly lessonEditorOpen = signal(false);
  readonly assignmentEditorOpen = signal(false);
  readonly editingLessonId = signal<string | null>(null);
  readonly editingAssignmentId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly gradeScores = signal<Record<string, string>>({});
  readonly gradeFeedbacks = signal<Record<string, string>>({});
  readonly moderationReasons = signal<Record<string, string>>({});
  courseDraft = { categoryId: '', title: '', summary: '', description: '' };
  lessonDraft = { title: '', content: '', position: 1 };
  assignmentDraft = { title: '', instructions: '', dueAtLocal: '', maxAttempts: 1, maxScore: 100 };

  ngOnInit(): void {
    forkJoin({ courses: this.api.teacherCourses(), categories: this.api.categories() }).subscribe({
      next: result => {
        this.courses.set([...result.courses].sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc)));
        this.categories.set(result.categories);
        this.loadingCourses.set(false);
        if (this.courses().length) this.selectCourse(this.courses()[0]);
      },
      error: err => { this.showError(err, 'Your teacher workspace could not be loaded.'); this.loadingCourses.set(false); },
    });
  }

  selectCourse(course: Course): void {
    this.selectedCourse.set(course); this.courseEditorOpen.set(false); this.activeTab.set('lessons');
    this.lessonEditorOpen.set(false); this.assignmentEditorOpen.set(false); this.loadWorkspace(course.id);
  }
  startNewCourse(): void {
    this.selectedCourse.set(null); this.courseDraft = { categoryId: this.categories()[0]?.id ?? '', title: '', summary: '', description: '' };
    this.courseEditorOpen.set(true); this.notice.set('');
  }
  editCourse(course: Course): void {
    this.selectedCourse.set(course); this.courseDraft = { categoryId: course.categoryId, title: course.title, summary: course.summary, description: course.description };
    this.courseEditorOpen.set(true); this.notice.set('');
  }
  cancelCourseEdit(): void { this.courseEditorOpen.set(false); }
  saveCourse(): void {
    if (this.saving()) return;
    const data = { ...this.courseDraft };
    this.saving.set(true); this.error.set('');
    const request = this.selectedCourse()
      ? this.api.updateTeacherCourse(this.selectedCourse()!.id, data)
      : this.api.createTeacherCourse(data);
    request.subscribe({
      next: course => {
        this.upsertCourse(course); this.courseEditorOpen.set(false); this.saving.set(false);
        this.notice.set('Course draft saved. Add lessons and assignments, then submit it for review.'); this.selectCourse(course);
      }, error: err => { this.showError(err, 'The course could not be saved.'); this.saving.set(false); },
    });
  }
  submitCourse(course: Course): void {
    this.runCourseAction(this.api.submitTeacherCourse(course.id), 'Course sent for admin review.');
  }
  archiveCourse(course: Course): void {
    if (!globalThis.confirm(`Archive “${course.title}”? Archived courses cannot be restored.`)) return;
    this.runCourseAction(this.api.archiveTeacherCourse(course.id), 'Course archived. Enrolled learners keep read access.');
  }
  statusLabel(status: string): string { return status === 'PendingApproval' ? 'In review' : status; }
  canEdit(course: Course): boolean { return ['Draft', 'Rejected', 'Published'].includes(course.status); }
  canManageContent(course: Course): boolean { return !['PendingApproval', 'Archived'].includes(course.status); }
  setTab(tab: TeacherTab): void { this.activeTab.set(tab); }
  ungradedCount(): number { return this.submissions().filter(item => item.score === null || item.score === undefined).length; }
  allSubmissions(): TeacherAssignmentSubmission[] { return [...this.submissions()].sort((a, b) => b.submittedAtUtc.localeCompare(a.submittedAtUtc)); }
  hiddenDiscussionCount(): number { return this.discussions().filter(post => post.isHidden).length; }
  assignmentTitle(id: string): string { return this.assignments().find(item => item.id === id)?.title ?? 'Assignment'; }
  maxScore(id: string): number { return this.assignments().find(item => item.id === id)?.maxScore ?? 100; }
  gradeScore(submission: TeacherAssignmentSubmission): string { return this.gradeScores()[submission.id] ?? (submission.score?.toString() ?? ''); }
  gradeFeedback(submission: TeacherAssignmentSubmission): string { return this.gradeFeedbacks()[submission.id] ?? (submission.feedback ?? ''); }
  setGradeScore(id: string, value: string): void { this.gradeScores.update(state => ({ ...state, [id]: value })); }
  setGradeFeedback(id: string, value: string): void { this.gradeFeedbacks.update(state => ({ ...state, [id]: value })); }
  moderationReason(id: string): string { return this.moderationReasons()[id] ?? ''; }
  setModerationReason(id: string, value: string): void { this.moderationReasons.update(state => ({ ...state, [id]: value })); }

  newLesson(): void {
    this.editingLessonId.set(null); this.lessonDraft = { title: '', content: '', position: this.lessons().length + 1 };
    this.lessonEditorOpen.set(true);
  }
  editLesson(lesson: Lesson): void {
    this.editingLessonId.set(lesson.id); this.lessonDraft = { title: lesson.title, content: lesson.content, position: lesson.position };
    this.lessonEditorOpen.set(true);
  }
  cancelLessonEdit(): void { this.lessonEditorOpen.set(false); this.editingLessonId.set(null); }
  saveLesson(): void {
    const courseId = this.selectedCourse()?.id; if (!courseId || this.saving()) return;
    this.saving.set(true); this.error.set('');
    const id = this.editingLessonId();
    const request = id ? this.api.updateTeacherLesson(id, this.lessonDraft) : this.api.createTeacherLesson(courseId, this.lessonDraft);
    request.subscribe({ next: () => { this.saving.set(false); this.cancelLessonEdit(); this.loadWorkspace(courseId); this.notice.set(id ? 'Lesson updated.' : 'Lesson added.'); }, error: err => { this.showError(err, 'The lesson could not be saved.'); this.saving.set(false); } });
  }
  deleteLesson(lesson: Lesson): void {
    if (!globalThis.confirm(`Delete lesson “${lesson.title}”?`)) return;
    this.api.deleteTeacherLesson(lesson.id).subscribe({ next: () => { const id = this.selectedCourse()?.id; if (id) this.loadWorkspace(id); this.notice.set('Lesson deleted.'); }, error: err => this.showError(err, 'The lesson could not be deleted.') });
  }
  newAssignment(): void {
    this.editingAssignmentId.set(null); this.assignmentDraft = { title: '', instructions: '', dueAtLocal: '', maxAttempts: 1, maxScore: 100 };
    this.assignmentEditorOpen.set(true);
  }
  editAssignment(assignment: Assignment): void {
    this.editingAssignmentId.set(assignment.id);
    this.assignmentDraft = { title: assignment.title, instructions: assignment.instructions, dueAtLocal: this.toLocalInput(assignment.dueAtUtc), maxAttempts: assignment.maxAttempts, maxScore: assignment.maxScore };
    this.assignmentEditorOpen.set(true);
  }
  cancelAssignmentEdit(): void { this.assignmentEditorOpen.set(false); this.editingAssignmentId.set(null); }
  saveAssignment(): void {
    const courseId = this.selectedCourse()?.id; if (!courseId || this.saving()) return;
    const data = { title: this.assignmentDraft.title, instructions: this.assignmentDraft.instructions, dueAtUtc: this.assignmentDraft.dueAtLocal ? new Date(this.assignmentDraft.dueAtLocal).toISOString() : null, maxAttempts: Number(this.assignmentDraft.maxAttempts), maxScore: Number(this.assignmentDraft.maxScore) };
    this.saving.set(true); this.error.set(''); const id = this.editingAssignmentId();
    const request = id ? this.api.updateTeacherAssignment(id, data) : this.api.createTeacherAssignment(courseId, data);
    request.subscribe({ next: () => { this.saving.set(false); this.cancelAssignmentEdit(); this.loadWorkspace(courseId); this.notice.set(id ? 'Assignment updated.' : 'Assignment created.'); }, error: err => { this.showError(err, 'The assignment could not be saved.'); this.saving.set(false); } });
  }
  gradeSubmission(submission: TeacherAssignmentSubmission): void {
    const score = Number(this.gradeScore(submission)); const max = this.maxScore(submission.assignmentId);
    if (!Number.isFinite(score) || score < 0 || score > max || this.saving()) { this.error.set(`Enter a score from 0 to ${max}.`); return; }
    this.saving.set(true); this.api.gradeTeacherSubmission(submission.id, score, this.gradeFeedback(submission)).subscribe({
      next: graded => { this.submissions.update(rows => rows.map(row => row.id === graded.id ? graded : row)); this.saving.set(false); this.notice.set('Feedback saved for the learner.'); },
      error: err => { this.showError(err, 'The grade could not be saved.'); this.saving.set(false); },
    });
  }
  moderatePost(post: DiscussionPost): void {
    const reason = post.isHidden ? '' : this.moderationReason(post.id).trim();
    if (!post.isHidden && !reason) { this.error.set('Add a short reason before hiding a post.'); return; }
    this.saving.set(true); this.api.moderateTeacherDiscussion(post.id, !post.isHidden, reason).subscribe({
      next: updated => { this.discussions.update(rows => rows.map(row => row.id === updated.id ? updated : row)); this.saving.set(false); this.notice.set(updated.isHidden ? 'Discussion post hidden.' : 'Discussion post restored.'); },
      error: err => { this.showError(err, 'The discussion post could not be updated.'); this.saving.set(false); },
    });
  }

  private loadWorkspace(courseId: string): void {
    this.loadingWorkspace.set(true);
    forkJoin({ lessons: this.api.teacherLessons(courseId), assignments: this.api.teacherAssignments(courseId), discussions: this.api.teacherDiscussions(courseId) }).subscribe({
      next: result => {
        this.lessons.set([...result.lessons].sort((a, b) => a.position - b.position)); this.assignments.set(result.assignments);
        this.discussions.set([...result.discussions].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
        const calls = result.assignments.map(item => this.api.teacherSubmissions(item.id));
        (calls.length ? forkJoin(calls) : of([] as TeacherAssignmentSubmission[][])).subscribe({
          next: lists => { this.submissions.set(lists.flat()); this.loadingWorkspace.set(false); },
          error: err => { this.showError(err, 'Assignments loaded, but submissions could not be fetched.'); this.submissions.set([]); this.loadingWorkspace.set(false); },
        });
      }, error: err => { this.showError(err, 'Course details could not be loaded.'); this.loadingWorkspace.set(false); },
    });
  }
  private runCourseAction(request: ReturnType<ApiService['submitTeacherCourse']>, message: string): void {
    this.saving.set(true); request.subscribe({ next: course => { this.upsertCourse(course); this.selectedCourse.set(course); this.saving.set(false); this.notice.set(message); }, error: err => { this.showError(err, 'That course action could not be completed.'); this.saving.set(false); } });
  }
  private upsertCourse(course: Course): void { this.courses.update(rows => [course, ...rows.filter(item => item.id !== course.id)]); }
  private toLocalInput(value?: string | null): string {
    if (!value) return '';
    const date = new Date(value); date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
  private showError(err: any, fallback: string): void { this.error.set(err?.error?.title ?? err?.error?.detail ?? err?.error?.message ?? fallback); this.notice.set(''); }
}
