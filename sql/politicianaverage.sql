use FacebookDebat;

select name, posts, comments, bad_comments,bad_comments*100/comments as pct, average_score
from (
select name,
	(select count(*) from dbo.Post po where po.page_id = pa.id ) as posts,
	(select count(*) from dbo.Post po inner join dbo.Comment c on po.id = c.post_id where po.page_id = pa.id ) as comments,
	(select count(*) from dbo.Post po inner join dbo.Comment c on po.id = c.post_id where po.page_id = pa.id and c.score < -2) as bad_comments,
	(select avg(score) from dbo.Post po inner join dbo.Comment c on po.id = c.post_id where po.page_id = pa.id ) as average_score
from FacebookDebat.dbo.Page pa
group by id, name) as a
where comments > 50
order by pct desc