import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { Course } from './models';
import { ApiService } from './api.service';

describe('ApiService admin course editing', () => {
  let api: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends course fields and the selected file as multipart form data', () => {
    const image = new File(['cover bytes'], 'cover.png', { type: 'image/png' });
    const course: Course = {
      id: 'course-1',
      categoryId: 'category-1',
      instructorUserId: 'teacher-1',
      title: 'Algebra',
      summary: 'Build confidence with algebra.',
      description: 'A complete algebra course.',
      imageUrl: 'https://res.cloudinary.test/algebra.jpg',
      duration: '6 weeks',
      price: 0,
      difficulty: 'Beginner',
      prerequisites: null,
      status: 'Published',
      rejectionReason: null,
      createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: '2026-01-02T00:00:00Z',
    };
    let saved: Course | undefined;

    api.adminUpdateCourse('course-1', {
      categoryId: 'category-1',
      title: 'Algebra',
      summary: 'Build confidence with algebra.',
      description: 'A complete algebra course.',
      duration: '6 weeks',
      price: 0,
      difficulty: 'Beginner',
      prerequisites: '',
    }, image).subscribe(value => saved = value);

    const request = http.expectOne(`${environment.apiUrl}/api/admin/courses/course-1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.has('Content-Type')).toBe(false);
    expect(request.request.body).toBeInstanceOf(FormData);
    const form = request.request.body as FormData;
    expect(form.get('categoryId')).toBe('category-1');
    expect(form.get('title')).toBe('Algebra');
    expect(form.get('summary')).toBe('Build confidence with algebra.');
    expect(form.get('description')).toBe('A complete algebra course.');
    expect(form.get('duration')).toBe('6 weeks');
    expect(form.get('price')).toBe('0');
    expect(form.get('difficulty')).toBe('Beginner');
    expect(form.get('prerequisites')).toBeNull();
    const attachedImage = form.get('image');
    expect(attachedImage).toBeInstanceOf(File);
    expect((attachedImage as File).name).toBe('cover.png');
    expect((attachedImage as File).type).toBe('image/png');
    expect((attachedImage as File).size).toBe(image.size);

    request.flush(course);
    expect(saved).toEqual(course);
  });

  it('omits the image field when only course information changes', () => {
    api.adminUpdateCourse('course-2', {
      categoryId: 'category-2',
      title: 'Physics',
      summary: 'A short summary.',
      description: 'A longer description.',
      duration: 'Self-paced',
      price: 29.5,
      difficulty: 'Intermediate',
      prerequisites: 'Basic arithmetic',
    }, null).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/api/admin/courses/course-2`);
    expect(request.request.method).toBe('PUT');
    const form = request.request.body as FormData;
    expect(form.get('image')).toBeNull();
    expect(form.get('duration')).toBe('Self-paced');
    expect(form.get('price')).toBe('29.5');
    expect(form.get('difficulty')).toBe('Intermediate');
    expect(form.get('prerequisites')).toBe('Basic arithmetic');
    request.flush({});
  });
});
