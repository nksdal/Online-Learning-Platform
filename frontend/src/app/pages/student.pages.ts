import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { Assignment, AssignmentSubmission, CourseProgress, DiscussionPost, Enrollment, LearningStreak, Lesson, RecommendedCourse } from '../core/models';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="student-wrap">
      <section class="student-welcome">
        <div><span class="eyebrow">YOUR LEARNING SPACE</span><h1>Good to see you,<br><em>{{ auth.user()?.firstName || 'learner' }}.</em></h1><p>Small steps add up. Pick up where you left off.</p></div>
        <div class="welcome-mark" aria-hidden="true">✳</div>
      </section>
      @if (error()) { <div class="state-card state-error"><strong>We couldn’t load your learning space.</strong><span>{{ error() }}</span><button class="text-button" (click)="load()">Try again</button></div> }
      @else if (loading()) { <div class="state-card"><span class="spinner"></span> Gathering your learning…</div> }
      @else {
        <section class="student-stats">
          <div class="streak-card"><span class="eyebrow">YOUR RHYTHM</span><div class="streak-number">{{ streak()?.currentStreakDays ?? 0 }} <small>day{{ streak()?.currentStreakDays === 1 ? '' : 's' }}</small><span>✦</span></div><p>{{ streak()?.learnedToday ? 'You showed up today. Keep that feeling.' : 'A little learning today can keep your rhythm going.' }}</p><div class="streak-footer"><span>LONGEST STREAK</span><b>{{ streak()?.longestStreakDays ?? 0 }} DAYS</b></div></div>
          <div class="student-stat"><span class="stat-icon">↗</span><span class="eyebrow">IN PROGRESS</span><strong>{{ enrollments().length }}</strong><span>course{{ enrollments().length === 1 ? '' : 's' }} in your library</span></div>
          <div class="student-stat stat-lime"><span class="stat-icon">◎</span><span class="eyebrow">COMPLETED LESSONS</span><strong>{{ completedLessons() }}</strong><span>and counting</span></div>
        </section>

        <section class="student-section">
          <div class="student-heading"><div><span class="eyebrow">PICK UP WHERE YOU LEFT OFF</span><h2>My courses</h2></div><a class="text-button" routerLink="/">Explore all courses ↗</a></div>
          @if (enrollments().length === 0) {
            <div class="empty-learning"><div class="empty-mark">◈</div><div><h3>Your next chapter is waiting.</h3><p>Enroll in a course to start learning and track your progress here.</p></div><a class="button button-dark" routerLink="/">Explore courses <span>↗</span></a></div>
          } @else {
            <div class="learning-grid">
              @for (item of enrollments(); track item.id; let index = $index) {
                <a class="learning-card" [routerLink]="['/learn', item.courseId]">
                  <div class="learning-card-art" [class]="'learning-card-art art-' + (index % 4) + (item.courseImageUrl ? ' has-photo' : '')">@if (item.courseImageUrl) { <img class="learning-card-photo" [src]="item.courseImageUrl" [alt]="item.courseTitle + ' course photo'" loading="lazy" decoding="async"> }<span>{{ item.courseStatus === 'Archived' ? 'ARCHIVED COURSE' : 'YOUR COURSE' }}</span><b>{{ index + 1 < 10 ? '0' + (index + 1) : index + 1 }}</b></div>
                  <div class="learning-card-content"><h3>{{ item.courseTitle }}</h3><p>{{ item.courseSummary }}</p><div class="progress-copy"><span>{{ progressFor(item.courseId)?.completedLessons ?? 0 }} / {{ progressFor(item.courseId)?.totalLessons ?? 0 }} lessons</span><b>{{ progressFor(item.courseId)?.completionPercentage ?? 0 }}%</b></div><div class="progress-track"><span [style.width.%]="progressFor(item.courseId)?.completionPercentage ?? 0"></span></div><span class="continue-link">{{ item.courseStatus === 'Archived' ? 'Review course' : 'Continue learning' }} <b>↗</b></span></div>
                </a>
              }
            </div>
          }
        </section>

        <section class="student-section recommendations-section">
          <div class="student-heading"><div><span class="eyebrow">A LITTLE MORE TO EXPLORE</span><h2>Picked for you</h2></div><span class="quiet-label">BASED ON WHAT YOU’RE LEARNING</span></div>
          @if (recommendations().length) {
            <div class="recommendation-grid">@for (item of recommendations(); track item.courseId; let index = $index) {
              <a class="recommendation-card" [routerLink]="['/courses', item.courseId]"><div [class]="'recommendation-mark art-' + (index % 4) + (item.imageUrl ? ' has-photo' : '')">@if (item.imageUrl) { <img class="recommendation-photo" [src]="item.imageUrl" [alt]="item.title + ' course photo'" loading="lazy" decoding="async"> } @else { <span>{{ ['✳', '⌘', '◈'][index % 3] }}</span> }</div><span class="eyebrow">{{ item.reason }}</span><h3>{{ item.title }}</h3><p>{{ item.summary }}</p><span class="continue-link">Take a look <b>↗</b></span></a>
            }</div>
          } @else { <div class="recommendations-empty">{{ recommendationsUnavailable() ? 'Suggestions are temporarily unavailable. Explore the catalogue for your next course.' : 'You’re all caught up. Explore the catalogue for your next course.' }}</div> }
        </section>
      }
    </main>
  `,
})
export class StudentDashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);
  readonly enrollments = signal<Enrollment[]>([]);
  readonly progress = signal<Record<string, CourseProgress | null>>({});
  readonly recommendations = signal<RecommendedCourse[]>([]);
  readonly recommendationsUnavailable = signal(false);
  readonly streak = signal<LearningStreak | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly completedLessons = signal(0);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set('');
    this.recommendationsUnavailable.set(false);
    forkJoin({
      enrollments: this.api.myEnrollments(),
      progress: this.api.myProgress().pipe(catchError(() => of([] as CourseProgress[]))),
      streak: this.api.myStreak(),
      recommendations: this.api.recommendations().pipe(catchError(() => {
        this.recommendationsUnavailable.set(true);
        return of([] as RecommendedCourse[]);
      })),
    }).subscribe({
      next: result => {
        this.enrollments.set(result.enrollments); this.streak.set(result.streak); this.recommendations.set(result.recommendations);
        const byCourse = new Map(result.progress.map(item => [item.courseId, item]));
        const next: Record<string, CourseProgress | null> = {};
        result.enrollments.forEach(enrollment => next[enrollment.courseId] = byCourse.get(enrollment.courseId) ?? null);
        this.progress.set(next);
        this.completedLessons.set(result.progress.reduce((total, row) => total + row.completedLessons, 0));
        this.loading.set(false);
      },
      error: err => { this.error.set(err?.error?.title ?? 'Your session may have expired. Sign in again, then retry.'); this.loading.set(false); },
    });
  }

  progressFor(courseId: string): CourseProgress | null { return this.progress()[courseId] ?? null; }
}

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="learning-wrap">
      <a class="back-link" routerLink="/my-learning">← Back to My Learning</a>
      @if (loading()) { <div class="state-card"><span class="spinner"></span> Opening your course…</div> }
      @else if (error()) { <div class="state-card state-error"><strong>Course lessons are unavailable.</strong><span>{{ error() }}</span><a class="text-button" routerLink="/my-learning">Return to My Learning</a></div> }
      @else if (enrollment(); as course) {
        <header class="learning-header"><span class="eyebrow">{{ course.courseStatus === 'Archived' ? 'ARCHIVED COURSE · READ ONLY' : 'YOUR LEARNING SPACE' }}</span><h1>{{ course.courseTitle }}</h1><p>{{ course.courseSummary }}</p><div class="learning-overview"><div class="progress-track"><span [style.width.%]="progress()?.completionPercentage ?? 0"></span></div><b>{{ progress()?.completionPercentage ?? 0 }}% complete</b><span>{{ progress()?.completedLessons ?? 0 }} of {{ progress()?.totalLessons ?? lessons().length }} lessons</span></div></header>
        <nav class="course-tabs" aria-label="Course sections">
          <button [class.active]="activeTab() === 'lessons'" (click)="setTab('lessons')">Lessons <span>{{ lessons().length }}</span></button>
          <button [class.active]="activeTab() === 'assignments'" (click)="setTab('assignments')">Assignments <span>{{ assignments().length }}</span></button>
          <button [class.active]="activeTab() === 'discussion'" (click)="setTab('discussion')">Discussion <span>{{ topLevelPosts().length }}</span></button>
        </nav>
        @if (activeTab() === 'lessons') {
          @if (lessons().length === 0) { <div class="state-card"><strong>Your teacher is preparing the lessons.</strong><span>Check back here soon.</span></div> }
          @else {
            <div class="lesson-layout"><aside class="lesson-list"><span class="eyebrow">COURSE CONTENT</span>@for (lesson of lessons(); track lesson.id; let i = $index) { <button class="lesson-row" [class.selected]="selectedLesson()?.id === lesson.id" (click)="selectLesson(lesson)"><span class="lesson-index">{{ progressItem(lesson.id)?.isCompleted ? '✓' : (i + 1 < 10 ? '0' + (i + 1) : i + 1) }}</span><span>{{ lesson.title }}</span></button> }</aside>
              <article class="lesson-view">@if (selectedLesson(); as lesson) { <span class="eyebrow">LESSON {{ lesson.position < 10 ? '0' + lesson.position : lesson.position }}</span><h2>{{ lesson.title }}</h2>@if (lesson.videoUrl) { <video class="lesson-video" controls preload="metadata" [src]="lesson.videoUrl">Your browser cannot play this lesson video.</video> }<div class="lesson-content">{{ lesson.content }}</div><div class="lesson-controls"><span>{{ progressItem(lesson.id)?.isCompleted ? 'Completed' : 'Your progress saves as you go' }}</span>@if (course.courseStatus === 'Published') { <button class="button" [class.button-dark]="!progressItem(lesson.id)?.isCompleted" [class.button-light]="progressItem(lesson.id)?.isCompleted" [disabled]="saving()" (click)="toggleComplete(lesson.id)">{{ saving() ? 'Saving…' : progressItem(lesson.id)?.isCompleted ? 'Mark incomplete' : 'Mark lesson complete' }} <span>✓</span></button> }</div> }</article>
            </div>
          }
        }
        @if (activeTab() === 'assignments') {
          @if (assignments().length === 0) { <div class="state-card"><strong>No assignments yet.</strong><span>When your instructor adds one, it will appear here.</span></div> }
          @else { <div class="assignment-list">@for (assignment of assignments(); track assignment.id) {
            <article class="assignment-card"><header class="assignment-heading"><div><span class="eyebrow">TEXT ASSIGNMENT</span><h2>{{ assignment.title }}</h2></div><span class="assignment-score">UP TO {{ assignment.maxScore | number:'1.1-2' }} PTS</span></header>
              <p class="assignment-instructions">{{ assignment.instructions }}</p>
              <div class="assignment-meta"><span>{{ assignment.dueAtUtc ? 'Due ' + (assignment.dueAtUtc | date:'medium') : 'No deadline' }}</span><span>{{ submissionsFor(assignment.id).length }} / {{ assignment.maxAttempts }} attempts</span></div>
              @if (submissionsFor(assignment.id).length) { <div class="attempt-list"><span class="eyebrow">YOUR ATTEMPTS</span>@for (attempt of submissionsFor(assignment.id); track attempt.id) {
                <div class="attempt-card"><div><b>Attempt {{ attempt.attemptNumber }}</b><time>{{ attempt.submittedAtUtc | date:'medium' }}</time></div>@if (attempt.text) { <p>{{ attempt.text }}</p> }@if (attempt.attachmentUrl) { <button class="submission-attachment-link" type="button" [disabled]="downloadingAttachment() === attempt.id" (click)="downloadAttachment(attempt.id, attempt.attachmentFileName || 'submission-file')">{{ downloadingAttachment() === attempt.id ? 'Downloading…' : '↓ ' + (attempt.attachmentFileName || 'Download attachment') }}</button> }@if (attachmentError() && downloadingAttachment() === null) { <p class="inline-error" role="alert">{{ attachmentError() }}</p> }@if (attempt.score !== null && attempt.score !== undefined) { <div class="grade-line"><strong>{{ attempt.score | number:'1.1-2' }} / {{ assignment.maxScore | number:'1.1-2' }}</strong><span>{{ attempt.feedback || 'Graded' }}</span></div> } @else { <span class="awaiting-grade">Waiting for your instructor’s feedback</span> }</div>
              }</div> }
              @if (course.courseStatus === 'Archived') { <p class="read-only-note">This course is archived. Your existing submissions remain available to review.</p> }
              @else if (deadlinePassed(assignment)) { <p class="read-only-note">The deadline has passed. You can still review previous attempts.</p> }
              @else if (submissionsFor(assignment.id).length >= assignment.maxAttempts) { <p class="read-only-note">You’ve used all attempts for this assignment.</p> }
              @else { <div class="submission-form"><label [for]="'submission-' + assignment.id">{{ submissionsFor(assignment.id).length ? 'Write another attempt' : 'Your response' }}</label><textarea [id]="'submission-' + assignment.id" rows="5" maxlength="20000" [value]="draftFor(assignment.id)" (input)="setDraft(assignment.id, $any($event.target).value)" placeholder="Write your response here…"></textarea><label class="submission-file-label">Attach a file (optional, up to 10 MB)<input type="file" [id]="'attachment-' + assignment.id" accept=".pdf,.docx,.xlsx,.pptx,.txt,.csv,.png,.jpg,.jpeg,.webp" (change)="selectAttachment(assignment.id, $event)"></label>@if (attachmentName(assignment.id)) { <div class="submission-file-selected"><span>{{ attachmentName(assignment.id) }}</span><button type="button" (click)="clearAttachment(assignment.id)">Remove</button></div> }@if (assignmentErrors()[assignment.id]) { <p class="inline-error" role="alert">{{ assignmentErrors()[assignment.id] }}</p> }<div class="submission-actions"><span>Up to {{ assignment.maxAttempts }} attempt{{ assignment.maxAttempts === 1 ? '' : 's' }}</span><button class="button button-dark" [disabled]="submittingAssignment() === assignment.id || (!draftFor(assignment.id).trim() && !attachmentFor(assignment.id))" (click)="submitAssignment(assignment.id)">{{ submittingAssignment() === assignment.id ? 'Submitting…' : attachmentFor(assignment.id) ? 'Submit response and file' : 'Submit response' }} <span>↗</span></button></div></div> }
            </article>
          }</div> }
        }
        @if (activeTab() === 'discussion') {
          <section class="discussion-panel">
            <div class="discussion-compose"><span class="eyebrow">SHARE A QUESTION OR IDEA</span><textarea rows="3" maxlength="5000" [value]="newPostDraft()" (input)="newPostDraft.set($any($event.target).value)" placeholder="What are you thinking about this course?"></textarea>@if (discussionError()) { <p class="inline-error" role="alert">{{ discussionError() }}</p> }@if (course.courseStatus === 'Published') { <div class="submission-actions"><span>Keep it kind and course-related.</span><button class="button button-dark" [disabled]="postingDiscussion() || !newPostDraft().trim()" (click)="createPost()">{{ postingDiscussion() ? 'Posting…' : 'Post to discussion' }} <span>↗</span></button></div> }@else { <p class="read-only-note">This course is archived. Discussions are available to read, but new posts are closed.</p> }</div>
            @if (topLevelPosts().length === 0) { <div class="state-card"><strong>Start the conversation.</strong><span>Ask a question or share something you learned.</span></div> }
            <div class="discussion-list">@for (post of topLevelPosts(); track post.id) {
              <article class="discussion-post"><div class="post-author"><span class="avatar-mark">{{ isCurrentUser(post.authorUserId) ? 'Y' : 'L' }}</span><div><b>{{ isCurrentUser(post.authorUserId) ? 'You' : 'Learner' }}</b><time>{{ post.createdAtUtc | date:'medium' }}</time></div></div><p>{{ post.body }}</p>
                @for (reply of repliesFor(post.id); track reply.id) { <div class="discussion-reply"><div class="post-author"><span class="avatar-mark reply-avatar">{{ isCurrentUser(reply.authorUserId) ? 'Y' : 'L' }}</span><div><b>{{ isCurrentUser(reply.authorUserId) ? 'You' : 'Learner' }}</b><time>{{ reply.createdAtUtc | date:'medium' }}</time></div></div><p>{{ reply.body }}</p></div> }
                @if (course.courseStatus === 'Published') { @if (replyingTo() === post.id) { <div class="reply-form"><textarea rows="2" maxlength="5000" [value]="replyDraft()" (input)="replyDraft.set($any($event.target).value)" placeholder="Write a reply…"></textarea><div class="submission-actions"><button class="text-button" (click)="cancelReply()">Cancel</button><button class="button button-dark" [disabled]="postingDiscussion() || !replyDraft().trim()" (click)="submitReply(post.id)">{{ postingDiscussion() ? 'Replying…' : 'Reply' }} <span>↗</span></button></div></div> } @else { <button class="reply-button" (click)="startReply(post.id)">Reply <span>↩</span></button> } }
              </article>
            }</div>
            @if (discussionPosts().length < discussionTotalCount()) { <div class="submission-actions"><span>Showing {{ discussionPosts().length }} of {{ discussionTotalCount() }} posts.</span><button class="button button-light" [disabled]="loadingOlderDiscussion()" (click)="loadOlderDiscussion()">{{ loadingOlderDiscussion() ? 'Loading…' : 'Load older posts' }}</button></div> }
          </section>
        }
      }
    </main>
  `,
})
export class LearningPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  readonly lessons = signal<Lesson[]>([]);
  readonly progress = signal<CourseProgress | null>(null);
  readonly enrollment = signal<Enrollment | null>(null);
  readonly assignments = signal<Assignment[]>([]);
  readonly submissions = signal<Record<string, AssignmentSubmission[]>>({});
  readonly assignmentDrafts = signal<Record<string, string>>({});
  readonly assignmentAttachments = signal<Record<string, File | null>>({});
  readonly assignmentErrors = signal<Record<string, string>>({});
  readonly submittingAssignment = signal<string | null>(null);
  readonly downloadingAttachment = signal<string | null>(null);
  readonly attachmentError = signal('');
  readonly discussionPosts = signal<DiscussionPost[]>([]);
  readonly topLevelPosts = computed(() => {
    const posts = this.discussionPosts();
    const ids = new Set(posts.map(post => post.id));
    return posts.filter(post => !post.parentPostId || !ids.has(post.parentPostId));
  });
  readonly repliesByParent = signal<Record<string, DiscussionPost[]>>({});
  readonly discussionPage = signal(1);
  readonly discussionTotalCount = signal(0);
  readonly loadingOlderDiscussion = signal(false);
  readonly activeTab = signal<'lessons' | 'assignments' | 'discussion'>('lessons');
  readonly newPostDraft = signal('');
  readonly replyDraft = signal('');
  readonly replyingTo = signal<string | null>(null);
  readonly postingDiscussion = signal(false);
  readonly discussionError = signal('');
  readonly selectedLesson = signal<Lesson | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  private courseId = '';

  ngOnInit(): void {
    this.courseId = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.myEnrollments().subscribe({
      next: enrollments => {
        const found = enrollments.find(item => item.courseId === this.courseId);
        if (!found) { this.error.set('You need to enroll in this course to view its lessons.'); this.loading.set(false); return; }
        this.enrollment.set(found);
        forkJoin({
          lessons: this.api.lessons(this.courseId),
          progress: this.api.courseProgress(this.courseId),
          assignments: this.api.assignments(this.courseId),
          discussions: this.api.discussions(this.courseId),
          submissions: this.api.myCourseSubmissions(this.courseId).pipe(catchError(() => of({} as Record<string, AssignmentSubmission[]>))),
        }).subscribe({
          next: result => {
            this.lessons.set([...result.lessons].sort((a, b) => a.position - b.position)); this.progress.set(result.progress);
            this.assignments.set(result.assignments);
            this.setDiscussionPosts([...result.discussions.items].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc)));
            this.discussionPage.set(result.discussions.page); this.discussionTotalCount.set(result.discussions.totalCount);
            this.selectedLesson.set(this.lessons().find(lesson => !this.progressItem(lesson.id)?.isCompleted) ?? this.lessons()[0] ?? null);
            this.submissions.set(result.submissions);
            this.loading.set(false);
          },
          error: err => { this.error.set(err?.error?.title ?? 'We couldn’t load this course.'); this.loading.set(false); },
        });
      },
      error: err => { this.error.set(err?.error?.title ?? 'Sign in as a student to view your course.'); this.loading.set(false); },
    });
  }

  progressItem(lessonId: string) { return this.progress()?.lessons.find(item => item.lessonId === lessonId); }
  selectLesson(lesson: Lesson): void { this.selectedLesson.set(lesson); }
  setTab(tab: 'lessons' | 'assignments' | 'discussion'): void { this.activeTab.set(tab); }
  submissionsFor(assignmentId: string): AssignmentSubmission[] { return this.submissions()[assignmentId] ?? []; }
  draftFor(assignmentId: string): string { return this.assignmentDrafts()[assignmentId] ?? ''; }
  setDraft(assignmentId: string, value: string): void { this.assignmentDrafts.update(current => ({ ...current, [assignmentId]: value })); }
  attachmentFor(assignmentId: string): File | null { return this.assignmentAttachments()[assignmentId] ?? null; }
  attachmentName(assignmentId: string): string { return this.attachmentFor(assignmentId)?.name ?? ''; }
  selectAttachment(assignmentId: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (!file) return;
    const allowed = ['.pdf', '.docx', '.xlsx', '.pptx', '.txt', '.csv', '.png', '.jpg', '.jpeg', '.webp'];
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!allowed.includes(extension)) { this.assignmentErrors.update(rows => ({ ...rows, [assignmentId]: 'Choose a PDF, Office document, TXT, CSV, or image file.' })); input.value = ''; return; }
    if (file.size > 10 * 1024 * 1024) { this.assignmentErrors.update(rows => ({ ...rows, [assignmentId]: 'Attachments must be 10 MB or smaller.' })); input.value = ''; return; }
    this.assignmentErrors.update(rows => ({ ...rows, [assignmentId]: '' }));
    this.assignmentAttachments.update(rows => ({ ...rows, [assignmentId]: file }));
  }
  clearAttachment(assignmentId: string): void {
    this.assignmentAttachments.update(rows => ({ ...rows, [assignmentId]: null }));
    const input = document.getElementById(`attachment-${assignmentId}`) as HTMLInputElement | null;
    if (input) input.value = '';
  }
  deadlinePassed(assignment: Assignment): boolean { return !!assignment.dueAtUtc && Date.parse(assignment.dueAtUtc) < Date.now(); }
  repliesFor(postId: string): DiscussionPost[] { return this.repliesByParent()[postId] ?? []; }
  loadOlderDiscussion(): void {
    if (this.loadingOlderDiscussion() || this.discussionPosts().length >= this.discussionTotalCount()) return;
    this.loadingOlderDiscussion.set(true); this.discussionError.set('');
    this.api.discussions(this.courseId, this.discussionPage() + 1).subscribe({
      next: page => {
        const merged = new Map([...this.discussionPosts(), ...page.items].map(post => [post.id, post]));
        this.setDiscussionPosts([...merged.values()].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc)));
        this.discussionPage.set(page.page); this.discussionTotalCount.set(page.totalCount); this.loadingOlderDiscussion.set(false);
      },
      error: err => { this.discussionError.set(err?.error?.title ?? 'Older discussion posts could not be loaded.'); this.loadingOlderDiscussion.set(false); },
    });
  }
  isCurrentUser(userId: string): boolean { return this.auth.user()?.id === userId; }

  submitAssignment(assignmentId: string): void {
    const text = this.draftFor(assignmentId).trim();
    const file = this.attachmentFor(assignmentId);
    if ((!text && !file) || this.submittingAssignment()) return;
    this.submittingAssignment.set(assignmentId);
    this.assignmentErrors.update(current => ({ ...current, [assignmentId]: '' }));
    (file ? this.api.submitAssignmentWithAttachment(assignmentId, text, file) : this.api.submitAssignment(assignmentId, text)).subscribe({
      next: submission => {
        this.submissions.update(current => ({ ...current, [assignmentId]: [...(current[assignmentId] ?? []), submission] }));
        this.assignmentDrafts.update(current => ({ ...current, [assignmentId]: '' }));
        this.clearAttachment(assignmentId);
        this.submittingAssignment.set(null);
      },
      error: err => {
        this.assignmentErrors.update(current => ({ ...current, [assignmentId]: err?.error?.title ?? 'The response could not be submitted. Check the deadline and attempt limit.' }));
        this.submittingAssignment.set(null);
      },
    });
  }

  downloadAttachment(submissionId: string, fileName: string): void {
    if (this.downloadingAttachment()) return;
    this.attachmentError.set('');
    this.downloadingAttachment.set(submissionId);
    this.api.downloadSubmissionAttachment(submissionId).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url; link.download = fileName; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 30_000);
        this.downloadingAttachment.set(null);
      },
      error: err => {
        this.attachmentError.set(err?.error?.title ?? 'The attachment could not be downloaded. Try again.');
        this.downloadingAttachment.set(null);
      },
    });
  }

  createPost(): void {
    const body = this.newPostDraft().trim();
    if (!body || this.postingDiscussion()) return;
    this.postingDiscussion.set(true); this.discussionError.set('');
    this.api.createDiscussionPost(this.courseId, body).subscribe({
      next: post => {
        this.discussionPosts.update(current => [...current, post]); this.discussionTotalCount.update(count => count + 1);
        this.newPostDraft.set(''); this.postingDiscussion.set(false);
      },
      error: err => { this.discussionError.set(err?.error?.title ?? 'Your post could not be published. Try again.'); this.postingDiscussion.set(false); },
    });
  }

  startReply(postId: string): void { this.replyingTo.set(postId); this.replyDraft.set(''); this.discussionError.set(''); }
  cancelReply(): void { this.replyingTo.set(null); this.replyDraft.set(''); }
  submitReply(postId: string): void {
    const body = this.replyDraft().trim();
    if (!body || this.postingDiscussion()) return;
    this.postingDiscussion.set(true); this.discussionError.set('');
    this.api.replyToDiscussion(postId, body).subscribe({
      next: reply => {
        this.discussionPosts.update(current => [...current, reply]);
        this.repliesByParent.update(current => ({ ...current, [postId]: [...(current[postId] ?? []), reply] }));
        this.discussionTotalCount.update(count => count + 1);
        this.cancelReply(); this.postingDiscussion.set(false);
      },
      error: err => { this.discussionError.set(err?.error?.title ?? 'Your reply could not be published. Try again.'); this.postingDiscussion.set(false); },
    });
  }

  private setDiscussionPosts(posts: DiscussionPost[]): void {
    this.discussionPosts.set(posts);
    const replies: Record<string, DiscussionPost[]> = {};
    for (const post of posts) {
      if (!post.parentPostId) continue;
      (replies[post.parentPostId] ??= []).push(post);
    }
    this.repliesByParent.set(replies);
  }

  toggleComplete(lessonId: string): void {
    const completed = this.progressItem(lessonId)?.isCompleted ?? false;
    this.saving.set(true);
    (completed ? this.api.resetLesson(lessonId) : this.api.completeLesson(lessonId)).subscribe({
      next: () => this.api.courseProgress(this.courseId).subscribe({
        next: result => { this.progress.set(result); this.saving.set(false); },
        error: () => this.saving.set(false),
      }),
      error: err => { this.error.set(err?.error?.title ?? 'We couldn’t save your progress. Try again.'); this.saving.set(false); },
    });
  }
}
