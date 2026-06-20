// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Helper functions to update badges
window.updateFavoritesBadge = function (delta) {
    var $navbarLinks = $('.js-favorites-link');

    $navbarLinks.each(function () {
        var $link = $(this);
        var $container = $link.find('.position-relative');
        var $badge = $container.find('.badge');

        if ($badge.length) {
            var currentCount = parseInt($badge.text()) || 0;
            var newCount = currentCount + delta;

            if (newCount > 0) {
                $badge.text(newCount);
            } else {
                $badge.remove();
            }
        } else if (delta > 0) {
            // Badge doesn't exist, create it matching _FavoritesCount partial view style
            $container.append('<span class="position-absolute badge rounded-circle d-flex align-items-center justify-content-center" style="top: -8px; right: -10px; background-color: #e74c3c; color: white; width: 18px; height: 18px; font-size: 0.7rem; border: 1px solid #232f3e;">1</span>');
        }
    });
};

window.updateBasketBadge = function (count) {
    var $navbarLinks = $('.js-basket-link');

    $navbarLinks.each(function () {
        var $link = $(this);
        var $container = $link.find('.position-relative');
        var $badge = $container.find('.badge');

        if ($badge.length) {
            if (count > 0) {
                $badge.text(count);
            } else {
                $badge.remove();
            }
        } else if (count > 0) {
            // Badge doesn't exist, create it matching _BasketCount partial view style
            $container.append('<span class="position-absolute badge rounded-circle d-flex align-items-center justify-content-center" style="top: -8px; right: -10px; background-color: #f68b1e; color: white; width: 18px; height: 18px; font-size: 0.7rem; border: 1px solid #232f3e;">' + count + '</span>');
        }
    });
};

window.showAppToast = function (type, message) {
    var toastEl = document.getElementById('liveToast');
    if (!toastEl || !window.bootstrap) return;

    var $toast = $(toastEl);
    var $message = $toast.find('.app-toast-message');
    var $icon = $toast.find('.app-toast-icon');

    $toast.removeClass('bg-success bg-danger bg-warning bg-info text-dark text-white');
    $icon.removeClass('fa-check-circle fa-times-circle fa-heart fa-info-circle fa-exclamation-triangle');

    if (type === 'danger') {
        $toast.addClass('bg-danger text-white');
        $icon.addClass('fa-times-circle');
    } else if (type === 'warning') {
        $toast.addClass('bg-warning text-dark');
        $icon.addClass('fa-exclamation-triangle');
    } else if (type === 'favorite') {
        $toast.addClass('bg-success text-white');
        $icon.addClass('fa-heart');
    } else {
        $toast.addClass('bg-success text-white');
        $icon.addClass('fa-check-circle');
    }

    $message.html(message);

    var toast = bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 3200 });
    toast.show();
};

$(document).ready(function () {
    function buildGoToBasketButton(isMiniButton) {
        if (isMiniButton) {
            return '<a href="/Basket/Index" class="btn-mini-cart btn-mini-cart-success" title="Sepete Git"><i class="fas fa-check"></i></a>';
        }

        return '<a href="/Basket/Index" class="btn btn-secondary w-100 mt-auto position-relative" style="z-index: 2; background-color: #28a745; border-color: #28a745; color: white;"><i class="fas fa-check me-2"></i>Sepete Git</a>';
    }

    // Generic AJAX Form Handler for Add to Cart
    $(document).on('submit', '.js-add-to-cart-form', function (e) {
        e.preventDefault();

        var $form = $(this);
        var $button = $form.find('button[type="submit"]');
        var originalContent = $button.html();

        // Disable button and show loading state
        $button.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

        $.ajax({
            url: $form.attr('action'),
            method: 'POST',
            data: $form.serialize(),
            headers: { "X-Requested-With": "XMLHttpRequest" },
            success: function (response) {
                if (response.success) {
                    window.showAppToast('success', '&Uuml;r&uuml;n sepete eklendi!');

                    // Update Basket Count Badge
                    if (response.count !== undefined) {
                        window.updateBasketBadge(response.count);
                    }

                    if ($form.closest('.product-card').length) {
                        $form.replaceWith(buildGoToBasketButton(true));
                    } else if ($form.attr('id') === 'addToCartForm') {
                        var detailButtonClasses = ($button.attr('class') || 'add-to-cart-btn') + ' in-cart';
                        $form.replaceWith(buildGoToBasketButton(false));
                    } else {
                        $button.prop('disabled', false).html(originalContent);
                        $button.addClass('in-cart');

                        var $icon = $button.find('i');
                        if ($icon.length) {
                            $icon.removeClass('fa-shopping-cart fa-cart-plus').addClass('fa-check');
                        }

                        var $textSpan = $button.find('span');
                        if ($textSpan.length) {
                            $textSpan.text('Sepete Git');
                        } else {
                            $button.html('<i class="fas fa-check"></i> Sepete Git');
                        }
                    }

                } else {
                    window.showAppToast('danger', 'Bir hata olu&#351;tu: ' + (response.message || 'Bilinmeyen hata'));
                    $button.prop('disabled', false).html(originalContent);
                }
            },
            error: function () {
                window.showAppToast('danger', 'Bir hata olu&#351;tu.');
                $button.prop('disabled', false).html(originalContent);
            }
        });
    });

    // Favorites Link Click Handler (Global)
    $(document).on('click', '.js-favorites-link', function (e) {
        if (!window.isAuthenticated) {
            e.preventDefault();
            window.showAppToast('warning', 'Favorilerinizi g&ouml;rmek i&ccedil;in l&uuml;tfen giri&#351; yap&#305;n.');
        }
    });

    // Favorite Button Toggle Handler (Global)
    $(document).on('click', '.btn-favorite', function (e) {
        e.preventDefault();
        e.stopPropagation(); // Prevent card link from triggering

        if (!window.isAuthenticated) {
            window.showAppToast('warning', 'Favorilere eklemek i&ccedil;in l&uuml;tfen giri&#351; yap&#305;n.');
            return;
        }

        var $button = $(this);
        var productId = $button.data('product-id');
        var $icon = $button.find('i');

        // Disable button temporarily
        $button.prop('disabled', true);

        // Determine action based on current state
        var isFavorite = $button.hasClass('active') || $icon.hasClass('fas');
        var url = isFavorite ? '/Favorites/Remove/' + productId : '/Favorites/Add/' + productId;

        // Get Verification Token if present
        var token = $('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: url,
            method: 'POST',
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "RequestVerificationToken": token
            },
            success: function (response) {
                if (!response.success) {
                    console.error('Favori iÅŸlemi baÅŸarÄ±sÄ±z oldu.');
                    $button.prop('disabled', false);
                    return;
                }
                // Toggle visual state
                if ($button.hasClass('active')) {
                    $button.removeClass('active');
                    $icon.removeClass('fas').addClass('far');
                    $button.attr('title', 'Favorilere Ekle');
                    window.updateFavoritesBadge(-1);
                    window.showAppToast('success', '&Uuml;r&uuml;n favorilerden kald&#305;r&#305;ld&#305;.');
                } else {
                    $button.addClass('active');
                    $icon.removeClass('far').addClass('fas');
                    $button.attr('title', 'Favorilerden Ã‡Ä±kar');
                    window.updateFavoritesBadge(1);
                    window.showAppToast('favorite', '&Uuml;r&uuml;n favorilere eklendi.');

                    // Heartbeat animation
                    $icon.css('transform', 'scale(1.2)');
                    setTimeout(() => $icon.css('transform', 'scale(1)'), 200);
                }

                $button.prop('disabled', false);
            },
            error: function () {
                console.error('Favori iÅŸlemi baÅŸarÄ±sÄ±z oldu.');
                $button.prop('disabled', false);
            }
        });
    });

    function setProductCardImage($gallery, index) {
        var images = $gallery.data('cardImagesParsed');
        if (!images || images.length === 0) return;

        var normalizedIndex = Math.max(0, Math.min(index, images.length - 1));
        var $image = $gallery.find('.product-img');
        $gallery.data('cardImageIndex', normalizedIndex);
        $image.attr('src', images[normalizedIndex]);
        $gallery.find('.product-card-image-dot')
            .removeClass('active')
            .eq(normalizedIndex)
            .addClass('active');

        $image.off('load.productCardOrientation').on('load.productCardOrientation', function () {
            var isPortrait = this.naturalHeight > this.naturalWidth * 1.1;
            $(this).toggleClass('product-img-portrait', isPortrait);
        });
    }

    $('.product-card-gallery').each(function () {
        var $gallery = $(this);
        var rawImages = $gallery.attr('data-card-images');
        var images = [];

        try {
            images = JSON.parse(rawImages || '[]');
        } catch (e) {
            images = [];
        }

        $gallery.data('cardImagesParsed', images);
        $gallery.data('cardImageIndex', 0);
    });

    $(document).on('mousemove', '.product-card-gallery', function (e) {
        var $gallery = $(this);
        var images = $gallery.data('cardImagesParsed');
        if (!images || images.length <= 1) return;

        var offset = $gallery.offset();
        var width = $gallery.outerWidth();
        if (!offset || !width) return;

        var relativeX = Math.max(0, Math.min(e.pageX - offset.left, width - 1));
        var nextIndex = Math.floor((relativeX / width) * images.length);

        if ($gallery.data('cardImageIndex') !== nextIndex) {
            setProductCardImage($gallery, nextIndex);
        }
    });

    $(document).on('mouseleave', '.product-card-gallery', function () {
        var $gallery = $(this);
        var images = $gallery.data('cardImagesParsed');
        if (!images || images.length <= 1) return;

        setProductCardImage($gallery, 0);
    });

    $(document).on('click', '.product-card', function (e) {
        if ($(e.target).closest('a, button, form, .btn-favorite, .js-add-to-cart-form').length) {
            return;
        }

        var url = $(this).data('product-url');
        if (url) {
            window.location.href = url;
        }
    });

    $(document).on('keydown', '.product-card', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') {
            return;
        }

        if ($(e.target).closest('a, button, form, .btn-favorite, .js-add-to-cart-form').length) {
            return;
        }

        e.preventDefault();

        var url = $(this).data('product-url');
        if (url) {
            window.location.href = url;
        }
    });

    // Swipeable Product Card Sliders (Mouse Grab & Drag support)
    function initCardSliders() {
        $('.product-card-slider').each(function () {
            var $slider = $(this);
            var $dots = $slider.siblings('.product-card-dots').find('.dot');
            var isDown = false;
            var startX;
            var scrollLeft;
            var dragThreshold = 8;
            var moved = false;

            // Update dots active state on scroll
            $slider.on('scroll', function () {
                var width = $slider.outerWidth();
                if (width <= 0) return;
                var scrollLeftVal = $slider.scrollLeft();
                var index = Math.round(scrollLeftVal / width);
                $dots.removeClass('active').eq(index).addClass('active');
            });

            // Mouse events
            $slider.on('mousedown', function (e) {
                isDown = true;
                $slider.css('cursor', 'grabbing');
                startX = e.pageX - $slider.offset().left;
                scrollLeft = $slider.scrollLeft();
                moved = false;
            });

            $slider.on('mouseleave mouseup', function () {
                isDown = false;
                $slider.css('cursor', 'grab');
            });

            $slider.on('mousemove', function (e) {
                if (!isDown) return;
                e.preventDefault();
                var x = e.pageX - $slider.offset().left;
                var walk = (x - startX) * 1.5; // Drag sensitivity multiplier
                if (Math.abs(walk) > dragThreshold) {
                    moved = true;
                }
                $slider.scrollLeft(scrollLeft - walk);
            });

            // Prevent link navigation if drag occurred on slider
            $slider.on('click', function (e) {
                if (moved) {
                    e.preventDefault();
                    e.stopPropagation();
                }
            });
        });
    }

    initCardSliders();
});
