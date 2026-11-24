import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { ApiService } from '../core/api.service';
import { AdminDashboardPage } from './admin.page';

describe('AdminDashboardPage course image selection', () => {
  it('rejects unsupported image formats before an upload request can be made', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: {} }],
    });
    const page = TestBed.runInInjectionContext(() => new AdminDashboardPage());
    const input = {
      files: [new File(['svg'], 'cover.svg', { type: 'image/svg+xml' })],
      value: 'C:\\fakepath\\cover.svg',
    } as unknown as HTMLInputElement;

    page.selectCourseImage({ target: input } as unknown as Event);

    expect(page.error()).toBe('Choose a JPEG, PNG or WebP image.');
    expect(page.selectedCourseImageName()).toBe('');
    expect(input.value).toBe('');
  });
});
