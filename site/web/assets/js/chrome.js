// The bar at the top and the sections' reveals, on every page of the site.

/** Wires the bar's menu and the reveals of the sections as they scroll into view. */
export function initChrome() {
  document.documentElement.classList.add('js');
  const nav = document.querySelector('.site-nav');
  const toggle = nav.querySelector('.nav-toggle');
  toggle?.addEventListener('click', () => {
    const open = nav.classList.toggle('open');
    toggle.setAttribute('aria-expanded', String(open));
  });
  nav.querySelectorAll('.nav-links a').forEach((link) => link.addEventListener('click', () => {
    nav.classList.remove('open');
    toggle?.setAttribute('aria-expanded', 'false');
  }));
  const onScroll = () => nav.classList.toggle('scrolled', window.scrollY > 40);
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  const reveal = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (entry.isIntersecting) {
        entry.target.classList.add('shown');
        reveal.unobserve(entry.target);
      }
    }
  }, { rootMargin: '0px 0px -8% 0px' });
  document.querySelectorAll('.reveal').forEach((el) => reveal.observe(el));
}
