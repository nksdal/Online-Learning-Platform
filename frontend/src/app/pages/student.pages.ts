import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
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
          } @else { <div class="recommendations-empty">You’re all caught up. Explore the catalogue for your next course.</div> }
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
  readonly streak = signal<LearningStreak | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly completedLessons = signal(0);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set('');
    forkJoin({ enrollments: this.api.myEnrollments(), streak: this.api.myStreak(), recommendations: this.api.recommendations() }).subscribe({
      next: result => {
        this.enrollments.set(result.enrollments); this.streak.set(result.streak); this.recommendations.set(result.recommendations);
        if (result.enrollments.length === 0) { this.progress.set({}); this.completedLessons.set(0); this.loading.set(false); return; }
        forkJoin(result.enrollments.map(item => this.api.courseProgress(item.courseId).pipe(catchError(() => of(null)))))
          .subscribe(rows => {
            const next: Record<string, CourseProgress | null> = {};
            result.enrollments.forEach((enrollment, index) => next[enrollment.courseId] = rows[index]);
            this.progress.set(next);
            this.completedLessons.set(rows.reduce((total, row) => total + (row?.completedLessons ?? 0), 0));
            this.loading.set(false);
          });
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
              <article class="lesson-view">@if (selectedLesson(); as lesson) { <span class="eyebrow">LESSON {{ lesson.position < 10 ? '0' + lesson.position : lesson.position }}</span><h2>{{ lesson.title }}</h2><div class="lesson-content">{{ lesson.content }}</div><div class="lesson-controls"><span>{{ progressItem(lesson.id)?.isCompleted ? 'Completed' : 'Your progress saves as you go' }}</span>@if (course.courseStatus === 'Published') { <button class="button" [class.button-dark]="!progressItem(lesson.id)?.isCompleted" [class.button-light]="progressItem(lesson.id)?.isCompleted" [disabled]="saving()" (click)="toggleComplete(lesson.id)">{{ saving() ? 'Saving…' : progressItem(lesson.id)?.isCompleted ? 'Mark incomplete' : 'Mark lesson complete' }} <span>✓</span></button> }</div> }</article>
            </div>
          }
        }
        @if (activeTab() === 'assignments') {
          @if (assignments().length === 0) { <div class="state-card"><strong>No assignments yet.</strong><span>When your instructor adds one, it will appear here.</span></div> }
          @else { <div class="assignment-list">@for (assignment of assignments(); track assignment.id) {
            <article class="assignment-card"><header class="assignment-heading"><div><span class="eyebrow">TEXT ASSIGNMENT</span><h2>{{ assignment.title }}</h2></div><span class="assignment-score">UP TO {{ assignment.maxScore }} PTS</span></header>
              <p class="assignment-instructions">{{ assignment.instructions }}</p>
              <div class="assignment-meta"><span>{{ assignment.dueAtUtc ? 'Due ' + (assignment.dueAtUtc | date:'medium') : 'No deadline' }}</span><span>{{ submissionsFor(assignment.id).length }} / {{ assignment.maxAttempts }} attempts</span></div>
              @if (submissionsFor(assignment.id).length) { <div class="attempt-list"><span class="eyebrow">YOUR ATTEMPTS</span>@for (attempt of submissionsFor(assignment.id); track attempt.id) {
                <div class="attempt-card"><div><b>Attempt {{ attempt.attemptNumber }}</b><time>{{ attempt.submittedAtUtc | date:'medium' }}</time></div><p>{{ attempt.text }}</p>@if (attempt.score !== null && attempt.score !== undefined) { <div class="grade-line"><strong>{{ attempt.score }} / {{ assignment.maxScore }}</strong><span>{{ attempt.feedback || 'Graded' }}</span></div> } @else { <span class="awaiting-grade">Waiting for your instructor’s feedback</span> }</div>
              }</div> }
              @if (course.courseStatus === 'Archived') { <p class="read-only-note">This course is archived. Your existing submissions remain available to review.</p> }
              @else if (deadlinePassed(assignment)) { <p class="read-only-note">The deadline has passed. You can still review previous attempts.</p> }
              @else if (submissionsFor(assignment.id).length >= assignment.maxAttempts) { <p class="read-only-note">You’ve used all attempts for this assignment.</p> }
              @else { <div class="submission-form"><label [for]="'submission-' + assignment.id">{{ submissionsFor(assignment.id).length ? 'Write another attempt' : 'Your response' }}</label><textarea [id]="'submission-' + assignment.id" rows="5" maxlength="20000" [value]="draftFor(assignment.id)" (input)="setDraft(assignment.id, $any($event.target).value)" placeholder="Write your response here…"></textarea>@if (assignmentErrors()[assignment.id]) { <p class="inline-error" role="alert">{{ assignmentErrors()[assignment.id] }}</p> }<div class="submission-actions"><span>Up to {{ assignment.maxAttempts }} attempt{{ assignment.maxAttempts === 1 ? '' : 's' }}</span><button class="button button-dark" [disabled]="submittingAssignment() === assignment.id || !draftFor(assignment.id).trim()" (click)="submitAssignment(assignment.id)">{{ submittingAssignment() === assignment.id ? 'Submitting…' : 'Submit response' }} <span>↗</span></button></div></div> }
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
  readonly assignmentErrors = signal<Record<string, string>>({});
  readonly submittingAssignment = signal<string | null>(null);
  readonly discussionPosts = signal<DiscussionPost[]>([]);
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
        }).subscribe({
          next: result => {
            this.lessons.set([...result.lessons].sort((a, b) => a.position - b.position)); this.progress.set(result.progress);
            this.assignments.set(result.assignments);
            this.discussionPosts.set([...result.discussions].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc)));
            this.selectedLesson.set(this.lessons().find(lesson => !this.progressItem(lesson.id)?.isCompleted) ?? this.lessons()[0] ?? null);
            const histories = result.assignments.length
              ? forkJoin(result.assignments.map(item => this.api.mySubmissions(item.id).pipe(catchError(() => of([] as AssignmentSubmission[])))))
              : of([] as AssignmentSubmission[][]);
            histories.subscribe(rows => {
              const next: Record<string, AssignmentSubmission[]> = {};
              result.assignments.forEach((item, index) => next[item.id] = rows[index] ?? []);
              this.submissions.set(next);
              this.loading.set(false);
            });
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
  deadlinePassed(assignment: Assignment): boolean { return !!assignment.dueAtUtc && Date.parse(assignment.dueAtUtc) < Date.now(); }
  topLevelPosts(): DiscussionPost[] { return this.discussionPosts().filter(post => !post.parentPostId); }
  repliesFor(postId: string): DiscussionPost[] { return this.discussionPosts().filter(post => post.parentPostId === postId); }
  isCurrentUser(userId: string): boolean { return this.auth.user()?.id === userId; }

  submitAssignment(assignmentId: string): void {
    const text = this.draftFor(assignmentId).trim();
    if (!text || this.submittingAssignment()) return;
    this.submittingAssignment.set(assignmentId);
    this.assignmentErrors.update(current => ({ ...current, [assignmentId]: '' }));
    this.api.submitAssignment(assignmentId, text).subscribe({
      next: submission => {
        this.submissions.update(current => ({ ...current, [assignmentId]: [...(current[assignmentId] ?? []), submission] }));
        this.assignmentDrafts.update(current => ({ ...current, [assignmentId]: '' }));
        this.submittingAssignment.set(null);
      },
      error: err => {
        this.assignmentErrors.update(current => ({ ...current, [assignmentId]: err?.error?.title ?? 'The response could not be submitted. Check the deadline and attempt limit.' }));
        this.submittingAssignment.set(null);
      },
    });
  }

  createPost(): void {
    const body = this.newPostDraft().trim();
    if (!body || this.postingDiscussion()) return;
    this.postingDiscussion.set(true); this.discussionError.set('');
    this.api.createDiscussionPost(this.courseId, body).subscribe({
      next: post => {
        this.discussionPosts.update(current => [...current, post]);
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
        this.cancelReply(); this.postingDiscussion.set(false);
      },
      error: err => { this.discussionError.set(err?.error?.title ?? 'Your reply could not be published. Try again.'); this.postingDiscussion.set(false); },
    });
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
