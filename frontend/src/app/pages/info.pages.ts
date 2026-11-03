import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Category, Course } from '../core/models';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="public-page-wrap">
      <header class="public-page-heading"><span class="nik-eyebrow">A PLACE TO START</span><h1>Explore courses.</h1><p>Choose a subject that sparks your curiosity. You can browse before you make an account.</p></header>
      <section class="public-course-section" aria-label="Course catalogue">
        <div class="nik-filter-row" aria-label="Filter courses by category"><button class="nik-filter" [class.selected]="!selectedCategory()" (click)="selectCategory(null)">All courses</button>@for (category of categories(); track category.id) { <button class="nik-filter" [class.selected]="selectedCategory() === category.id" (click)="selectCategory(category.id)">{{ category.name }}</button> }</div>
        @if (loading()) { <div class="nik-state"><span class="nik-spinner"></span> Finding courses for you…</div> }
        @else if (error()) { <div class="nik-state nik-state-error"><strong>We couldn’t load the course catalogue.</strong><span>{{ error() }}</span><button class="nik-retry" (click)="load()">Try again</button></div> }
        @else if (courses().length === 0) { <div class="nik-state"><strong>No courses in this category yet.</strong><span>Try another subject or check back soon.</span></div> }
        @else { <div class="nik-course-grid">@for (course of courses(); track course.id; let i = $index) { <a class="nik-course-card" [routerLink]="['/courses', course.id]"><div class="nik-course-art" [class]="'nik-course-art course-tone-' + (i % 4)"><span class="nik-course-index">{{ (i + 1).toString().padStart(2, '0') }} / COURSE</span><span class="nik-course-glyph">{{ glyph(i) }}</span><span class="nik-course-subject">{{ categoryName(course.categoryId) }}</span></div><div class="nik-course-body"><h2>{{ course.title }}</h2><p>{{ course.summary }}</p><span class="nik-course-link">Explore course <b>↗</b></span></div></a> }</div> }
      </section>
      <section class="public-page-cta"><div><span class="nik-eyebrow">READY WHEN YOU ARE</span><h2>Your next small win is waiting.</h2></div><a class="nik-button nik-button-navy" routerLink="/register">Create your account <span>↗</span></a></section>
    </main>
  `,
})
export class CoursesPage implements OnInit {
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
      next: value => { this.courses.set(value.courses); this.categories.set(value.categories); this.loading.set(false); },
      error: err => { this.error.set(err?.error?.title ?? 'Check that the API is running, then try again.'); this.loading.set(false); },
    });
  }
  selectCategory(id: string | null): void { this.selectedCategory.set(id); this.load(); }
  categoryName(id: string): string { return this.categories().find(category => category.id === id)?.name ?? 'Learning'; }
  glyph(index: number): string { return ['✳', '⌘', '◈', '◌'][index % 4]; }
}

@Component({
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="how-page-wrap">
      <section class="how-hero"><span class="nik-eyebrow">YOUR JOURNEY, MADE CLEAR</span><h1>Learning can feel<br /><em>simple again.</em></h1><p>Nikladori gives you a clear place to begin, a steady way to practice, and support when questions come up.</p><a class="nik-button nik-button-mint" routerLink="/courses">Explore courses <span>↗</span></a><span class="how-hero-mark" aria-hidden="true">N</span></section>
      <section class="how-steps"><header><span class="nik-eyebrow">THREE STEPS, ONE GOOD START</span><h2>From curious to confident.</h2></header><div class="how-step-grid">
        <article><span class="how-step-number">01</span><span class="how-step-icon step-course">⌕</span><h3>Find a subject you like</h3><p>Browse published courses by subject and see what each one covers before you enroll.</p><a routerLink="/courses">Browse the course list <b>↗</b></a></article>
        <article><span class="how-step-number">02</span><span class="how-step-icon step-learn">↗</span><h3>Learn in small steps</h3><p>Work through lessons at your own pace. Your progress is saved, so you can come back any time.</p><a routerLink="/register">Create a learner account <b>↗</b></a></article>
        <article><span class="how-step-number">03</span><span class="how-step-icon step-grow">✳</span><h3>Practice and get feedback</h3><p>Submit assignments, review teacher feedback, and ask course questions in the discussion space.</p><a routerLink="/courses">Choose your first course <b>↗</b></a></article>
      </div></section>
      <section class="how-support"><div><span class="nik-eyebrow">SUPPORT THAT FITS THE COURSE</span><h2>Teachers help make progress feel possible.</h2><p>Each course is guided by its teacher. Use course discussions to ask a question, and check assignment feedback when it’s ready.</p></div><div class="how-support-card"><span class="how-support-badge">✓</span><b>A little help at the right time</b><p>Your course has one place for lessons, assignments, discussion, and feedback.</p></div></section>
      <section class="how-finish"><span class="nik-eyebrow">WHEN YOU’RE READY</span><h2>Start with one lesson.</h2><p>Pick a course that interests you and take your first step.</p><a class="nik-button nik-button-navy" routerLink="/courses">Explore courses <span>↗</span></a></section>
    </main>
  `,
})
export class HowItWorksPage {}
