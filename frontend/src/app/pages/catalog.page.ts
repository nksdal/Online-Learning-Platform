import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Category, Course } from '../core/models';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main>
      <section class="nik-hero">
        <div class="nik-hero-inner">
          <div class="nik-hero-copy">
            <span class="nik-kicker"><span></span> A little progress, every day</span>
            <h1>Learn something<br />that <em>moves you forward.</em></h1>
            <p>Clear lessons, supportive teachers, and a learning path that makes your next step feel possible.</p>
            <div class="nik-hero-actions">
              <a class="nik-button nik-button-mint" routerLink="/register">Start learning <span aria-hidden="true">↗</span></a>
              <a class="nik-text-link" href="#courses">Explore courses <span aria-hidden="true">↓</span></a>
            </div>
            <div class="nik-hero-note"><span class="nik-check">✓</span> Learn at your own pace, from any device</div>
          </div>

          <div class="nik-visual" aria-label="A preview of the Nikladori learning dashboard">
            <div class="nik-orbit nik-orbit-a"></div><div class="nik-orbit nik-orbit-b"></div>
            <div class="nik-float-label"><span class="nik-mini-spark">✳</span> YOUR NEXT CHAPTER</div>
            <div class="nik-dashboard-card">
              <div class="nik-dash-top"><span class="nik-dash-brand"><span class="nik-dash-mark">N</span> nikladori</span><span class="nik-dash-avatar">S</span></div>
              <span class="nik-dash-label">A SAMPLE LEARNING PATH</span>
              <h2>Keep your<br />curiosity close.</h2>
              <div class="nik-progress-label"><span>Getting started</span><b>3 of 5 lessons</b></div>
              <div class="nik-progress"><span></span></div>
              <div class="nik-next-lesson"><span class="nik-play">▶</span><span><b>Next up</b><small>Build a strong foundation</small></span><span class="nik-arrow">↗</span></div>
            </div>
            <div class="nik-sticker">One lesson<br />at a time.</div>
            <span class="nik-spark nik-spark-one" aria-hidden="true">✦</span><span class="nik-spark nik-spark-two" aria-hidden="true">✳</span>
          </div>
        </div>
        <div class="nik-hero-bottom"><span>MADE FOR CURIOUS MINDS</span><span>YOUR PACE. YOUR POTENTIAL.</span></div>
      </section>

      <section class="nik-benefits page-wrap" id="how-it-works">
        <div class="nik-section-intro"><span class="nik-eyebrow">A BETTER WAY TO LEARN</span><h2>Small steps add up<br />to <em>big confidence.</em></h2><p>Learning should feel clear, encouraging, and easy to return to.</p></div>
        <div class="nik-benefit-grid">
          <article class="nik-benefit-card"><span class="nik-benefit-icon icon-mint">01</span><h3>Know what to do next</h3><p>Follow lessons in a thoughtful order, and keep your progress in one simple place.</p><span class="nik-card-arrow">↗</span></article>
          <article class="nik-benefit-card"><span class="nik-benefit-icon icon-blue">02</span><h3>Learn without the rush</h3><p>Study when it works for you. Pick up where you left off and make the pace your own.</p><span class="nik-card-arrow">↗</span></article>
          <article class="nik-benefit-card"><span class="nik-benefit-icon icon-lilac">03</span><h3>Get help along the way</h3><p>Ask questions, share ideas, and get feedback from the teachers guiding your course.</p><span class="nik-card-arrow">↗</span></article>
        </div>
      </section>

      <section class="nik-proof page-wrap">
        <div class="nik-proof-panel">
          <div class="nik-proof-copy"><span class="nik-eyebrow">BUILT FOR REAL PROGRESS</span><h2>Your effort has<br />somewhere to go.</h2><p>From your first lesson to your finished assignment, your learning stays connected. See your progress, revisit feedback, and keep moving forward.</p><a class="nik-proof-link" href="#courses">Find a course that fits <span>↗</span></a></div>
          <div class="nik-proof-board">
            <div class="nik-board-heading"><span>YOUR LEARNING, IN ONE PLACE</span><span class="nik-live-dot">✓ YOUR ACCOUNT</span></div>
            <div class="nik-board-row"><span class="nik-board-icon board-mint">✓</span><span><b>Lessons that build on each other</b><small>Clear steps, saved as you go</small></span><span class="nik-board-status">READY</span></div>
            <div class="nik-board-row"><span class="nik-board-icon board-blue">↗</span><span><b>Progress you can come back to</b><small>Your completed lessons stay with you</small></span><span class="nik-board-status">SAVED</span></div>
            <div class="nik-board-row"><span class="nik-board-icon board-lilac">✎</span><span><b>Feedback from your teacher</b><small>Assignments and course discussions</small></span><span class="nik-board-status">CONNECTED</span></div>
            <div class="nik-board-foot"><span>Made for your next small win</span><span class="nik-board-star">✳</span></div>
          </div>
        </div>
      </section>

      <section class="nik-catalog page-wrap" id="courses">
        <div class="nik-catalog-heading"><div><span class="nik-eyebrow">START WITH WHAT SPARKS YOUR INTEREST</span><h2>Find your next skill.</h2><p>Explore courses built to turn curiosity into confidence.</p></div><span class="nik-course-total">{{ courses().length }} <small>COURSES</small></span></div>
        <div class="nik-filter-row" aria-label="Filter by category"><button class="nik-filter" [class.selected]="!selectedCategory()" (click)="selectCategory(null)">All courses</button>@for (category of categories(); track category.id) { <button class="nik-filter" [class.selected]="selectedCategory() === category.id" (click)="selectCategory(category.id)">{{ category.name }}</button> }</div>
        @if (loading()) { <div class="nik-state"><span class="nik-spinner"></span> Finding courses for you…</div> }
        @else if (error()) { <div class="nik-state nik-state-error"><strong>We couldn’t load the course catalogue.</strong><span>{{ error() }}</span><button class="nik-retry" (click)="load()">Try again</button></div> }
        @else if (courses().length === 0) { <div class="nik-state"><strong>No courses here just yet.</strong><span>Try another category or check back soon.</span></div> }
        @else { <div class="nik-course-grid">@for (course of courses(); track course.id; let i = $index) { <a class="nik-course-card" [routerLink]="['/courses', course.id]"><div class="nik-course-art" [class]="'nik-course-art course-tone-' + (i % 4) + (course.imageUrl ? ' has-photo' : '')">@if (course.imageUrl) { <img class="nik-course-photo" [src]="course.imageUrl" [alt]="course.title + ' course photo'" loading="lazy" decoding="async"> } @else { <span class="nik-course-glyph">{{ glyph(i) }}</span> }<span class="nik-course-index">{{ (i + 1).toString().padStart(2, '0') }} / COURSE</span><span class="nik-course-subject">{{ categoryName(course.categoryId) }}</span></div><div class="nik-course-body"><h3>{{ course.title }}</h3><p>{{ course.summary }}</p><span class="nik-course-link">Explore course <b>↗</b></span></div></a> }</div> }
      </section>

      <section class="nik-faq page-wrap">
        <div class="nik-faq-heading"><span class="nik-eyebrow">GOOD QUESTIONS</span><h2>Before you begin.</h2><p>A few helpful answers for your first visit.</p></div>
        <div class="nik-faq-list">
          <details><summary>Who is Nikladori for?<span>+</span></summary><p>Nikladori is designed for curious learners, especially pupils, who want a clear and supportive way to build new skills.</p></details>
          <details><summary>Can I learn at my own pace?<span>+</span></summary><p>Yes. Your lessons and progress stay available in your account, so you can continue when it suits you.</p></details>
          <details><summary>How do I ask a question about a course?<span>+</span></summary><p>When you’re enrolled, you can use the course discussion area to share a question or reply to classmates.</p></details>
          <details><summary>Do I need an account to explore?<span>+</span></summary><p>You can browse the available courses first. Create a free account when you’re ready to enroll and save your learning progress.</p></details>
        </div>
      </section>

      <section class="nik-final-cta"><div class="nik-cta-shape" aria-hidden="true">✳</div><div class="nik-cta-content"><span class="nik-eyebrow">YOUR NEXT CHAPTER STARTS HERE</span><h2>Ready for your next<br /><em>small win?</em></h2><p>Choose a course. Take the first lesson. See where it leads.</p><a class="nik-button nik-button-navy" routerLink="/register">Start learning <span aria-hidden="true">↗</span></a></div><span class="nik-cta-foot">NIKLADORI · LEARN WITH PURPOSE</span></section>
    </main>
  `,
})
export class CatalogPage implements OnInit {
  private readonly api = inject(ApiService);
  readonly courses = signal<Course[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly selectedCategory = signal<string | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true); this.error.set('');
    forkJoin({ courses: this.api.courses(this.selectedCategory() ?? undefined), categories: this.api.categories() }).subscribe({
      next: result => { this.courses.set(result.courses); this.categories.set(result.categories); this.loading.set(false); },
      error: err => { this.error.set(err?.error?.title ?? 'Check that the API is running, then try again.'); this.loading.set(false); },
    });
  }

  selectCategory(id: string | null): void { this.selectedCategory.set(id); this.load(); }
  categoryName(id: string): string { return this.categories().find(category => category.id === id)?.name ?? 'Learning'; }
  glyph(index: number): string { return ['✳', '⌘', '◈', '◌'][index % 4]; }
}
