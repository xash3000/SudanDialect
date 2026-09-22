import { Component, ElementRef, NgZone, OnDestroy, AfterViewInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap, takeUntil, tap } from 'rxjs/operators';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { SearchBarComponent } from '../../components/search-bar/search-bar.component';
import { WordCardComponent } from '../../components/word-card/word-card.component';
import { WordSearchResult } from '../../models/word-search-result';
import { WordVectorNeighbor } from '../../models/word-vector-neighbor';
import { WordSearchService } from '../../services/word-search.service';

interface WordMeshUserData {
  neighbor: WordVectorNeighbor;
}

@Component({
  selector: 'app-meaning-space-page',
  imports: [SearchBarComponent, WordCardComponent],
  templateUrl: './meaning-space-page.component.html',
  styleUrl: './meaning-space-page.component.css'
})
export class MeaningSpacePageComponent implements AfterViewInit, OnDestroy {
  @ViewChild('threeCanvas', { static: true }) private canvasRef!: ElementRef<HTMLDivElement>;

  private readonly ngZone = inject(NgZone);
  private readonly wordSearchService = inject(WordSearchService);
  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  // Search bar state
  protected readonly searchQuery = signal('');
  protected readonly searchResults = signal<WordSearchResult[]>([]);
  protected readonly isSearchLoading = signal(false);
  protected readonly isInputFocused = signal(false);
  protected readonly hasSearchError = signal(false);
  protected readonly showDropdown = computed(() => {
    const hasQuery = this.searchQuery().trim().length > 0;
    return this.isInputFocused() && hasQuery;
  });
  protected readonly shouldShowNoResults = computed(() => {
    return (
      !this.isSearchLoading() &&
      !this.hasSearchError() &&
      this.searchQuery().trim().length > 0 &&
      this.searchResults().length === 0
    );
  });

  // Space state
  protected readonly isSpaceLoading = signal(false);
  protected readonly hasSelectedWord = signal(false);
  protected readonly selectedNeighbor = signal<WordVectorNeighbor | null>(null);
  protected readonly spaceError = signal('');

  // Three.js objects
  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private renderer!: THREE.WebGLRenderer;
  private controls!: OrbitControls;
  private raycaster = new THREE.Raycaster();
  private mouse = new THREE.Vector2();
  private animationFrameId = 0;
  private wordMeshes: THREE.Mesh[] = [];
  private connectionLines: THREE.Line[] = [];
  private labelSprites: THREE.Sprite[] = [];
  private hoveredMesh: THREE.Mesh | null = null;
  private resizeObserver?: ResizeObserver;

  // Device pixel ratio used for canvas texture sharpness
  private readonly dpr = Math.min(window.devicePixelRatio, 2);

  constructor() {
    this.searchInput$
      .pipe(
        debounceTime(150),
        distinctUntilChanged(),
        tap(() => {
          this.isSearchLoading.set(true);
          this.hasSearchError.set(false);
        }),
        switchMap((query) =>
          this.wordSearchService.search(query).pipe(
            catchError(() => {
              this.hasSearchError.set(true);
              return of<WordSearchResult[]>([]);
            })
          )
        ),
        takeUntil(this.destroy$)
      )
      .subscribe((results) => {
        this.searchResults.set(results);
        this.isSearchLoading.set(false);
      });
  }

  ngAfterViewInit(): void {
    this.ngZone.runOutsideAngular(() => {
      const container = this.canvasRef.nativeElement;
      // Defer initialization until the container has non-zero dimensions.
      const ro = new ResizeObserver((entries) => {
        const { width, height } = entries[0].contentRect;
        if (width > 0 && height > 0) {
          ro.disconnect();
          this.initThreeScene();
          this.animate();
        }
      });
      ro.observe(container);
      this.resizeObserver = ro;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.searchInput$.complete();

    if (this.animationFrameId) {
      cancelAnimationFrame(this.animationFrameId);
    }

    this.controls?.dispose();
    this.renderer?.dispose();

    this.resizeObserver?.disconnect();
  }

  // Search bar handlers
  protected onSearchInput(value: string): void {
    this.searchQuery.set(value);
    const trimmed = value.trim();
    if (!trimmed) {
      this.searchResults.set([]);
      this.isSearchLoading.set(false);
      this.hasSearchError.set(false);
      return;
    }
    this.searchInput$.next(trimmed);
  }

  protected onSearchFocus(): void {
    this.isInputFocused.set(true);
    const trimmed = this.searchQuery().trim();
    if (trimmed) {
      this.searchInput$.next(trimmed);
    }
  }

  protected onSearchBlur(): void {
    this.isInputFocused.set(false);
  }

  protected onSearchPressed(): void {
    // no-op for mobile
  }

  protected onWordSelected(word: WordSearchResult): void {
    this.searchQuery.set(word.headword);
    this.searchResults.set([]);
    this.isInputFocused.set(false);
    this.selectedNeighbor.set(null);
    this.loadMeaningSpace(word.id);
  }

  protected clearSelectedNeighbor(): void {
    this.selectedNeighbor.set(null);
  }

  // Three.js initialization
  private initThreeScene(): void {
    const container = this.canvasRef.nativeElement;
    const width = container.clientWidth;
    const height = container.clientHeight;

    // Scene
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0xe1c094);

    // Camera
    this.camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 1000);
    this.camera.position.set(0, 0, 5);

    // WebGL Renderer — the sole rendering surface; labels live inside it
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.setSize(width, height);
    this.renderer.setPixelRatio(this.dpr);
    this.renderer.domElement.style.position = 'absolute';
    this.renderer.domElement.style.inset = '0';
    container.appendChild(this.renderer.domElement);

    // Controls
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.minDistance = 1;
    this.controls.maxDistance = 50;

    // Lights
    this.scene.add(new THREE.AmbientLight(0xffffff, 0.6));
    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(5, 10, 7);
    this.scene.add(directionalLight);

    // Click handler
    this.renderer.domElement.addEventListener('click', this.onCanvasClick);
    // Hover handler
    this.renderer.domElement.addEventListener('mousemove', this.onCanvasMouseMove);

    // Resize handler — observe the container directly for accurate sizing
    this.resizeObserver = new ResizeObserver(this.onWindowResize);
    this.resizeObserver.observe(container);
  }

  private animate = (): void => {
    this.animationFrameId = requestAnimationFrame(this.animate);
    this.controls.update();
    this.renderer.render(this.scene, this.camera);
  };

  private onWindowResize = (): void => {
    const container = this.canvasRef.nativeElement;
    const width = container.clientWidth;
    const height = container.clientHeight;
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height);
  };

  private onCanvasClick = (event: MouseEvent): void => {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    this.raycaster.setFromCamera(this.mouse, this.camera);
    const intersects = this.raycaster.intersectObjects(this.wordMeshes);

    if (intersects.length > 0) {
      const mesh = intersects[0].object as THREE.Mesh;
      const userData = mesh.userData as WordMeshUserData;
      this.ngZone.run(() => {
        this.selectedNeighbor.set(userData.neighbor);
      });
    } else {
      this.ngZone.run(() => {
        this.selectedNeighbor.set(null);
      });
    }
  };

  private onCanvasMouseMove = (event: MouseEvent): void => {
    const rect = this.renderer.domElement.getBoundingClientRect();
    this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    this.raycaster.setFromCamera(this.mouse, this.camera);
    const intersects = this.raycaster.intersectObjects(this.wordMeshes);

    // Reset previous hover
    if (this.hoveredMesh) {
      const mat = this.hoveredMesh.material as THREE.MeshStandardMaterial;
      mat.emissive.setHex(0x000000);
      this.renderer.domElement.style.cursor = 'grab';
      this.hoveredMesh = null;
    }

    if (intersects.length > 0) {
      const mesh = intersects[0].object as THREE.Mesh;
      const mat = mesh.material as THREE.MeshStandardMaterial;
      mat.emissive.setHex(0x444444);
      this.renderer.domElement.style.cursor = 'pointer';
      this.hoveredMesh = mesh;
    }
  };

  // Meaning space loading
  private loadMeaningSpace(wordId: string): void {
    this.isSpaceLoading.set(true);
    this.hasSelectedWord.set(true);
    this.spaceError.set('');

    this.wordSearchService
      .getVectorNeighbors(wordId, 20)
      .pipe(
        catchError(() => {
          this.ngZone.run(() => {
            this.spaceError.set('تعذر تحميل فضاء المعاني.');
            this.isSpaceLoading.set(false);
          });
          return of(null);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((response) => {
        if (!response) {
          return;
        }

        this.ngZone.runOutsideAngular(() => {
          this.buildScene(response.words);
        });

        this.ngZone.run(() => {
          this.isSpaceLoading.set(false);
        });
      });
  }

  private buildScene(words: WordVectorNeighbor[]): void {
    // Clear existing objects
    this.clearScene();

    if (words.length === 0) {
      return;
    }

    // Find the selected word for connection lines
    const selectedWord = words.find((w) => w.isSelected);

    // Scale factor to spread out points nicely
    const scale = 8;

    for (const word of words) {
      const isSelected = word.isSelected;
      const radius = isSelected ? 0.35 : 0.12 + word.similarityScore * 0.18;
      const color = isSelected ? 0xd4ad7a : 0x4a88b5;

      // Sphere
      const geometry = new THREE.SphereGeometry(radius, 24, 24);
      const material = new THREE.MeshStandardMaterial({
        color,
        metalness: 0.3,
        roughness: 0.6
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.position.set(word.x * scale, word.y * scale, word.z * scale);
      (mesh.userData as WordMeshUserData) = { neighbor: word };
      this.scene.add(mesh);
      this.wordMeshes.push(mesh);

      // In-world sprite label (canvas texture rendered in WebGL — no DOM overlay)
      const sprite = this.createTextSprite(word.headword, isSelected);
      sprite.position.set(word.x * scale, word.y * scale + radius + 0.25, word.z * scale);
      this.scene.add(sprite);
      this.labelSprites.push(sprite);

      // Connection line from selected word to this neighbor
      if (selectedWord && !isSelected) {
        const lineGeometry = new THREE.BufferGeometry().setFromPoints([
          new THREE.Vector3(selectedWord.x * scale, selectedWord.y * scale, selectedWord.z * scale),
          new THREE.Vector3(word.x * scale, word.y * scale, word.z * scale)
        ]);
        const lineMaterial = new THREE.LineBasicMaterial({
          color: 0x1b4d7e,
          transparent: true,
          opacity: 0.06 + word.similarityScore * 0.2
        });
        const line = new THREE.Line(lineGeometry, lineMaterial);
        this.scene.add(line);
        this.connectionLines.push(line);
      }
    }

    // Position camera to see all points
    if (selectedWord) {
      this.controls.target.set(
        selectedWord.x * scale,
        selectedWord.y * scale,
        selectedWord.z * scale
      );
    } else {
      this.controls.target.set(0, 0, 0);
    }
    this.camera.position.set(
      (selectedWord?.x ?? 0) * scale + 6,
      (selectedWord?.y ?? 0) * scale + 4,
      (selectedWord?.z ?? 0) * scale + 6
    );
    this.controls.update();
  }

  /**
   * Creates a billboard THREE.Sprite whose texture is text drawn on an HTML Canvas,
   * so the label lives fully inside the WebGL canvas in 3D world space.
   */
  private createTextSprite(text: string, isSelected: boolean): THREE.Sprite {
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d')!;

    // Keep baseFontSize large so the canvas texture has plenty of pixels —
    // this controls texture resolution/sharpness, NOT the visible size in the scene.
    // Visible size is controlled exclusively by sprite.scale below.
    const baseFontSize = isSelected ? 96 : 80;
    const fontSize = baseFontSize * this.dpr;
    const fontWeight = isSelected ? '700' : '400';
    // Use the same Amiri font loaded by the page; canvas falls back to serif if
    // the font face has not yet resolved, but the font is preconnected in index.html
    // so it is available by the time the user interacts.
    const fontFamily = '"Amiri", serif';

    ctx.font = `${fontWeight} ${fontSize}px ${fontFamily}`;
    const metrics = ctx.measureText(text);
    const textWidth = metrics.width;

    const paddingX = fontSize * 0.4;
    const paddingY = fontSize * 0.3;
    canvas.width = Math.ceil(textWidth + paddingX * 2);
    canvas.height = Math.ceil(fontSize + paddingY * 2);

    // Re-apply font after canvas resize (resize resets canvas state)
    ctx.font = `${fontWeight} ${fontSize}px ${fontFamily}`;
    ctx.textBaseline = 'middle';
    ctx.textAlign = 'center';
    ctx.direction = 'rtl';

    // Text only — no background
    ctx.fillStyle = isSelected ? '#3a312e' : '#1b4d7e';
    ctx.fillText(text, canvas.width / 2, canvas.height / 2);

    const texture = new THREE.CanvasTexture(canvas);
    texture.needsUpdate = true;

    const spriteMaterial = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      depthTest: false,   // always visible — not occluded by spheres or lines
      depthWrite: false
    });

    const sprite = new THREE.Sprite(spriteMaterial);

    // sprite.scale controls the visible world-space size of the label.
    // Reduce these values to make labels appear smaller; increase for larger.
    const worldHeight = isSelected ? 0.45 : 0.32;
    const worldWidth = worldHeight * (canvas.width / canvas.height);
    sprite.scale.set(worldWidth, worldHeight, 1);

    return sprite;
  }

  private clearScene(): void {
    for (const mesh of this.wordMeshes) {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
      this.scene.remove(mesh);
    }
    this.wordMeshes = [];

    for (const line of this.connectionLines) {
      line.geometry.dispose();
      (line.material as THREE.Material).dispose();
      this.scene.remove(line);
    }
    this.connectionLines = [];

    for (const sprite of this.labelSprites) {
      const mat = sprite.material as THREE.SpriteMaterial;
      mat.map?.dispose();
      mat.dispose();
      this.scene.remove(sprite);
    }
    this.labelSprites = [];
  }
}
