using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class AlbumService : IAlbumService
{
    private readonly IUnitOfWork _unitOfWork;

    public AlbumService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AlbumDto>> GetAllAlbumsAsync()
    {
        var albums = await _unitOfWork.Repository<Album>().GetAllAsync();
        return albums.Select(a => new AlbumDto
        {
            AlbumId = a.AlbumId,
            Title = a.Title,
            AlbumType = a.AlbumType,
            CoverImageUrl = a.CoverImageUrl,
            ReleaseDate = a.ReleaseDate
        });
    }

    public async Task<AlbumDto?> GetAlbumByIdAsync(int id)
    {
        var a = await _unitOfWork.Repository<Album>().GetByIdAsync(id);
        if (a == null) return null;
        return new AlbumDto 
        { 
            AlbumId = a.AlbumId, 
            Title = a.Title, 
            AlbumType = a.AlbumType,
            CoverImageUrl = a.CoverImageUrl,
            ReleaseDate = a.ReleaseDate 
        };
    }

    public async Task CreateAlbumAsync(AlbumDto dto)
    {
        var album = new Album 
        { 
            Title = dto.Title, 
            AlbumType = dto.AlbumType,
            CoverImageUrl = dto.CoverImageUrl,
            ReleaseDate = dto.ReleaseDate 
        };
        await _unitOfWork.Repository<Album>().AddAsync(album);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdateAlbumAsync(AlbumDto dto)
    {
        var album = await _unitOfWork.Repository<Album>().GetByIdAsync(dto.AlbumId);
        if (album != null)
        {
            album.Title = dto.Title;
            album.AlbumType = dto.AlbumType;
            album.CoverImageUrl = dto.CoverImageUrl;
            album.ReleaseDate = dto.ReleaseDate;
            _unitOfWork.Repository<Album>().Update(album);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeleteAlbumAsync(int id)
    {
        var album = await _unitOfWork.Repository<Album>().GetByIdAsync(id);
        if (album != null)
        {
            _unitOfWork.Repository<Album>().Remove(album);
            await _unitOfWork.CompleteAsync();
        }
    }
}
