import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { Course } from '../core/models';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="detail-wrap">
      <a class="back-link" routerLink="/">← Back to all courses</a>
      @if (loading()) { <div class="state-card"><span class="spinner"></span> Opening course…</div> }
      @else if (error()) { <div class="state-card state-error"><strong>Course unavailable</strong><span>{{ error() }}</span><a class="text-button" routerLink="/">Return to catalogue</a></div> }
      @else if (course(); as item) {
        <section class="detail-hero">
          <div class="detail-copy"><span class="eyebrow">A COURSE FOR YOUR NEXT CHAPTER</span><h1>{{ item.title }}</h1><p class="detail-summary">{{ item.summary }}</p>
            <div class="detail-actions">
              @if (enrolled()) { <span class="enrolled-pill">✓ You’re enrolled</span> }
              @else { <button class="button button-dark" [disabled]="enrolling()" (click)="enroll()">{{ enrolling() ? 'Joining…' : 'Enroll in this course' }} <span>↗</span></button> }
            </div>
            @if (notice()) { <p class="inline-notice" role="status">{{ notice() }}</p> }
            @if (failure()) { <p class="inline-error" role="alert">{{ failure() }}</p> }
          </div>
          <div class="detail-visual" aria-hidden="true"><span>FIELD NOTES</span><div class="detail-shape">✳</div><b>LEARN<br>BY DOING</b></div>
        </section>
        <section class="detail-body"><div><span class="eyebrow">THE COURSE</span><h2>Make room for what’s next.</h2></div><div class="description">{{ item.description }}</div></section>
        <section class="detail-meta"><span>SELF-PACED LEARNING</span><span>BUILT FOR REAL LIFE</span><span>START WHEN YOU’RE READY</span></section>
      }
    </main>
  `,
})
export class CourseDetailPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly course = signal<Course | null>(null);
  readonly loading = signal(true);
  readonly enrolling = signal(false);
  readonly enrolled = signal(false);
  readonly error = signal('');
  readonly failure = signal('');
  readonly notice = signal('');
  private courseId = '';

  ngOnInit(): void {
    this.courseId = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.course(this.courseId).subscribe({
      next: course => { this.course.set(course); this.loading.set(false); this.checkEnrollment(); },
      error: err => { this.error.set(err?.error?.title ?? 'We couldn’t find this published course.'); this.loading.set(false); },
    });
  }

  private checkEnrollment(): void {
    if (!this.auth.isAuthenticated()) return;
    this.api.myEnrollments().subscribe({ next: rows => this.enrolled.set(rows.some(row => row.courseId === this.courseId)), error: () => {} });
  }

  async enroll(): Promise<void> {
    if (!this.auth.isAuthenticated()) { await this.router.navigate(['/login'], { queryParams: { returnUrl: `/courses/${this.courseId}` } }); return; }
    const role = this.auth.user()?.roles[0];
    if (role && role !== 'Student') { this.failure.set('Enrollment is available to student accounts.'); return; }
    this.enrolling.set(true); this.failure.set(''); this.notice.set('');
    try {
      await firstValueFrom(this.api.enroll(this.courseId));
      this.enrolled.set(true); this.notice.set('You’re in. Your course is ready when you are.');
    } catch (err: any) {
      this.failure.set(err?.error?.title ?? 'We couldn’t complete enrollment. Please try again.');
    } finally { this.enrolling.set(false); }
  }
}
