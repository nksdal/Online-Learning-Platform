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
    <main class="page-wrap">
      <section class="hero">
        <div class="hero-copy">
          <span class="eyebrow"><span class="eyebrow-dot"></span> LEARN AT YOUR OWN PACE</span>
          <h1>Small steps.<br><em>Big possibilities.</em></h1>
          <p>Build practical skills with focused courses, thoughtful instructors, and a learning path that fits your life.</p>
          <a class="button button-light" href="#catalog">Explore courses <span aria-hidden="true">↓</span></a>
        </div>
        <div class="hero-art" aria-hidden="true">
          <div class="orbit orbit-one"></div><div class="orbit orbit-two"></div>
          <div class="hero-card"><span class="hero-card-icon">✳</span><span>YOUR NEXT<br>CHAPTER</span><b>↗</b></div>
          <span class="spark spark-a">✦</span><span class="spark spark-b">✳</span>
        </div>
        <div class="hero-foot"><span>CURIOUS MINDS WELCOME</span><span>01 — 04</span></div>
      </section>

      <section class="catalog-section" id="catalog">
        <div class="section-heading">
          <div><span class="eyebrow">A GOOD PLACE TO BEGIN</span><h2>Find your next skill</h2></div>
          <span class="course-count">{{ courses().length }} COURSES</span>
        </div>
        <div class="filter-row" aria-label="Filter by category">
          <button class="filter-chip" [class.active]="!selectedCategory()" (click)="selectCategory(null)">All courses</button>
          @for (category of categories(); track category.id) {
            <button class="filter-chip" [class.active]="selectedCategory() === category.id" (click)="selectCategory(category.id)">{{ category.name }}</button>
          }
        </div>

        @if (loading()) { <div class="state-card"><span class="spinner"></span> Finding courses for you…</div> }
        @else if (error()) { <div class="state-card state-error"><strong>We couldn’t load the course catalogue.</strong><span>{{ error() }}</span><button class="text-button" (click)="load()">Try again</button></div> }
        @else if (courses().length === 0) { <div class="state-card"><strong>No courses here yet.</strong><span>Try another category or check back soon.</span></div> }
        @else {
          <div class="course-grid">
            @for (course of courses(); track course.id; let i = $index) {
              <a class="course-card" [routerLink]="['/courses', course.id]">
                <div class="course-art" [class]="'course-art art-' + (i % 4)">
                  <span class="course-number">{{ (i + 1).toString().padStart(2, '0') }}</span>
                  <span class="course-glyph">{{ glyph(i) }}</span>
                  <span class="art-label">{{ categoryName(course.categoryId) }}</span>
                </div>
                <div class="course-info"><h3>{{ course.title }}</h3><p>{{ course.summary }}</p><span class="card-link">Explore course <b>↗</b></span></div>
              </a>
            }
          </div>
        }
      </section>
      <section class="bottom-note"><span>KEEP GOING, AT YOUR OWN RHYTHM.</span><span>LEARNING IS A PRACTICE, NOT A RACE.</span></section>
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
